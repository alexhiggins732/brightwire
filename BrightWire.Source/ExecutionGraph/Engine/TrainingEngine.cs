﻿using BrightWire.ExecutionGraph.Engine.Helper;
using BrightWire.ExecutionGraph.Helper;
using BrightWire.ExecutionGraph.Node.Input;
using BrightWire.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BrightWire.ExecutionGraph.Engine
{
    /// <summary>
    /// Trains graphs as it executes them
    /// </summary>
    class TrainingEngine : EngineBase, IGraphTrainingEngine
    {
        readonly List<(IMiniBatchSequence Sequence, double? TrainingError, FloatMatrix Output)> _executionResults = new List<(IMiniBatchSequence Sequence, double? TrainingError, FloatMatrix Output)>();
        readonly List<IContext> _contextList = new List<IContext>();
	    readonly IReadOnlyList<INode> _input;
	    readonly bool _isStochastic;
        float? _lastTestError = null;
        double? _lastTrainingError = null, _trainingErrorDelta = null;

        public TrainingEngine(ILinearAlgebraProvider lap, IDataSource dataSource, ILearningContext learningContext, INode start) : base(lap)
        {
            _dataSource = dataSource;
            _isStochastic = lap.IsStochastic;
            LearningContext = learningContext;
            learningContext.SetRowCount(dataSource.RowCount);

            if(start == null) {
                _input = Enumerable.Range(0, dataSource.InputCount).Select(i => new InputFeeder(i)).ToList();
                Start = new FlowThrough();
                Start.Output.AddRange(_input.Select(i => new WireToNode(i)));
            }else {
                Start = start;
                _input = start.Output.Select(w => w.SendTo).ToList();
            }
        }

        public IReadOnlyList<ExecutionResult> Execute(IDataSource dataSource, int batchSize = 128, Action<float> batchCompleteCallback = null)
        {
            _lap.PushLayer();
            var ret = new List<ExecutionResult>();
            var provider = new MiniBatchProvider(dataSource, _isStochastic);
            using (var executionContext = new ExecutionContext(_lap)) {
                executionContext.Add(provider.GetMiniBatches(batchSize, mb => _Execute(executionContext, mb)));
                float operationCount = executionContext.RemainingOperationCount;
                float index = 0f;
                IGraphOperation operation;
                while ((operation = executionContext.GetNextOperation()) != null) {
                    _lap.PushLayer();
                    operation.Execute(executionContext);
                    _ClearContextList();
                    foreach (var item in _executionResults)
                        ret.Add(new ExecutionResult(item.Sequence, item.Output.Row));
                    _executionResults.Clear();
                    _lap.PopLayer();

                    if (batchCompleteCallback != null) {
                        var percentage = (++index) / operationCount;
                        batchCompleteCallback(percentage);
                    }
                }
            }
            _lap.PopLayer();
            return ret;
        }

        protected override IReadOnlyList<ExecutionResult> _GetResults()
        {
            var ret = new List<ExecutionResult>();
            foreach (var item in _executionResults)
                ret.Add(new ExecutionResult(item.Sequence, item.Output.Row));
            _executionResults.Clear();
            return ret;
        }

        protected override void _ClearContextList()
        {
            foreach (var item in _contextList)
                item.Dispose();
            _contextList.Clear();
        }

        public double Train(IExecutionContext executionContext, Action<float> batchCompleteCallback = null)
        {
            _lap.PushLayer();
            LearningContext.StartEpoch();
            var provider = new MiniBatchProvider(_dataSource, _isStochastic);
            executionContext.Add(provider.GetMiniBatches(LearningContext.BatchSize, batch => _contextList.AddRange(_Train(executionContext, LearningContext, batch))));

            IGraphOperation operation;
            float operationCount = executionContext.RemainingOperationCount;
            float index = 0f;
            while ((operation = executionContext.GetNextOperation()) != null) {
                _lap.PushLayer();
                operation.Execute(executionContext);
                LearningContext.ApplyUpdates();
                _ClearContextList();
                _lap.PopLayer();

                if (batchCompleteCallback != null) {
                    var percentage = (++index) / operationCount;
                    batchCompleteCallback(percentage);
                }
            }

            double ret = 0, count = 0;
            foreach (var item in _executionResults) {
	            if (item.TrainingError.HasValue)
	            {
		            ret += item.TrainingError.Value;
		            ++count;
	            }
            }
            if (count > 0)
                ret /= count;

            if (_lastTrainingError.HasValue)
                _trainingErrorDelta = ret - _lastTrainingError.Value;
            _lastTrainingError = ret;
            LearningContext.EndEpoch();
            _executionResults.Clear();
            _lap.PopLayer();
            return ret;
        }

        public IDataSource DataSource => _dataSource;
        public ILearningContext LearningContext { get; }
	    public INode GetInput(int index) => _input[index];
        public Models.ExecutionGraph Graph => Start.GetGraph();
        public ILinearAlgebraProvider LinearAlgebraProvider => _lap;
        public INode Start { get; }

	    protected override void _Execute(IExecutionContext executionContext, IMiniBatch batch)
        {
            _contextList.AddRange(_Train(executionContext, null, batch));
        }

        IReadOnlyList<IContext> _Train(IExecutionContext executionContext, ILearningContext learningContext, IMiniBatch batch)
        {
            var ret = new List<TrainingEngineContext>();
            if (batch.IsSequential) {
                IMiniBatchSequence curr;
                while ((curr = batch.GetNextSequence()) != null)
                    ret.Add(_Train(executionContext, learningContext, curr));

                var contextTable = new Lazy<Dictionary<IMiniBatchSequence, TrainingEngineContext>>(() => ret.ToDictionary(c => c.BatchSequence, c => c));
                var didContinue = _Continue(batch, executionContext, sequence => contextTable.Value[sequence]);
                if(didContinue) {
                    foreach (var context in ret)
                        _CompleteSequence(context);
                }
            } else
                ret.Add(_Train(executionContext, learningContext, batch.CurrentSequence));
            return ret;
        }

        void _CompleteSequence(TrainingEngineContext context)
        {
            _dataSource.OnBatchProcessed(context);
            _executionResults.Add((context.BatchSequence, context.TrainingError, context.Data.GetMatrix().Data));
        }

        TrainingEngineContext _Train(IExecutionContext executionContext, ILearningContext learningContext, IMiniBatchSequence sequence)
        {
            var context = new TrainingEngineContext(executionContext, sequence, learningContext);
            Start.ExecuteForward(context, 0);

            while (context.HasNext)
                context.ExecuteNext();

            if (!executionContext.HasContinuations)
                _CompleteSequence(context);
            return context;
        }

        public bool Test(
	        IDataSource testDataSource, 
	        IErrorMetric errorMetric, 
	        int batchSize = 128, 
	        Action<float> batchCompleteCallback = null,
			Action<float, double, bool, bool> values = null
	    ) {
            var testError = Execute(testDataSource, batchSize, batchCompleteCallback)
                .Where(b => b.Target != null)
                .Average(o => o.CalculateError(errorMetric))
            ;
            
            bool flag = true, isPercentage = errorMetric.DisplayAsPercentage;
            if (_lastTestError.HasValue) {
                if (isPercentage && _lastTestError.Value > testError)
                    flag = false;
                else if (!isPercentage && _lastTestError.Value < testError)
                    flag = false;
                else
                    _lastTestError = testError;
            } else
                _lastTestError = testError;

	        values?.Invoke(testError, _lastTrainingError ?? 0, isPercentage, flag);
			var outputType = isPercentage ? "score" : "error";
            if (LearningContext.CurrentEpoch == 0) {
                var score = String.Format(isPercentage ? "{0:P}" : "{0:N4}", testError);
	            LearningContext.MessageLog($"\rInitial test {outputType}: {score}");
                return false;
            } else {
                var format = isPercentage
                    ? "\rEpoch {0} - training-error: {1:N4} [{2:N4}]; time: {3:N2}s; test-{5}: {4:P}"
					: "\rEpoch {0} - training-error: {1:N4} [{2:N4}]; time: {3:N2}s; test-{5}: {4:N4}"
				;
                var msg = String.Format(format,
                    LearningContext.CurrentEpoch,
                    _lastTrainingError ?? 0,
                    _trainingErrorDelta,
                    LearningContext.EpochSeconds,
                    testError,
					outputType
				);
                if (flag)
                    msg += "!!";
	            LearningContext.MessageLog(msg);
                return flag;
            }
        }

        void _LoadParamaters(Models.ExecutionGraph.Node nodeModel)
        {
            var node = Start.FindById(nodeModel.Id);
            node.LoadParameters(nodeModel);
        }

        public void LoadParametersFrom(Models.ExecutionGraph graph)
        {
            if (graph.InputNode != null)
                _LoadParamaters(graph.InputNode);
            if (graph.OtherNodes != null) {
                foreach (var node in graph.OtherNodes)
                    _LoadParamaters(node);
            }
        }
    }
}
