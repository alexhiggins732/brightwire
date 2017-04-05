﻿using BrightWire;
using BrightWire.Descriptor.GradientDescent;
using BrightWire.ExecutionGraph;
using BrightWire.ExecutionGraph.Activation;
using BrightWire.ExecutionGraph.Component;
using BrightWire.ExecutionGraph.ErrorMetric;
using BrightWire.ExecutionGraph.Input;
using BrightWire.ExecutionGraph.Layer;
using BrightWire.ExecutionGraph.Wire;
using BrightWire.TrainingData.Artificial;
using System;
using System.IO;
using System.Linq;

namespace ExampleCode
{
    class Program
    {
        //static void XorTest()
        //{
        //    using (var lap = Provider.CreateLinearAlgebra()) {
        //        var graph = new GraphFactory(lap);
        //        var errorMetric = graph.ErrorMetric.Rmse;
        //        var data = Xor.Get();

        //        // create the property set
        //        var propertySet = graph.CreatePropertySet();
        //        propertySet.Use(new RmsPropDescriptor(0.9f));

        //        var feeder = graph.CreateInput(data);
        //        var engine = graph.CreateTrainingEngine(0.03f, 2, feeder);

        //        const int HIDDEN_LAYER_SIZE = 4;
        //        var network = graph.Connect(feeder, propertySet)
        //            .AddFeedForward(HIDDEN_LAYER_SIZE)
        //            .Add(graph.Activation.Sigmoid)
        //            .AddFeedForward(null)
        //            .Add(graph.Activation.Sigmoid)
        //            .AddBackpropagation(errorMetric)
        //            .Build()
        //        ;

        //        for (var i = 0; i < 1000; i++) {
        //            var trainingError = engine.Train();
        //            if (i % 100 == 0)
        //                engine.WriteTestResults(errorMetric);
        //        }
        //        engine.WriteTestResults(errorMetric);

        //        var testData = data.SelectColumns(Enumerable.Range(0, 2));
        //        var testInput = graph.CreateInput(testData, testData.GetVectoriser(false));
        //        testInput.AddTarget(network);
        //        var executionEngine = graph.CreateEngine(testInput);
        //        var results = executionEngine.Execute();
        //    }
        //}

        static void IrisTest()
        {
            var data = new StreamReader(new FileStream(@"D:\data\iris.txt", FileMode.Open)).ParseCSV().Split(0);
            using (var lap = Provider.CreateLinearAlgebra(false)) {
                var graph = new GraphFactory(lap);
                var errorMetric = graph.ErrorMetric.OneHotEncoding;

                // create the property set
                var propertySet = graph.CreatePropertySet();
                propertySet.Use(new AdamDescriptor());

                // create the traiing data
                var trainingData = graph.CreateInput(data.Training);
                var testData = graph.CreateInput(data.Test);
                var engine = graph.CreateTrainingEngine(0.01f, 32, trainingData, testData);

                const int HIDDEN_LAYER_SIZE = 4;
                var network = graph.Connect(trainingData, propertySet)
                    .AddFeedForward(HIDDEN_LAYER_SIZE)
                    .Add(graph.Activation.Sigmoid)
                    .AddFeedForward()
                    .Add(graph.Activation.SoftMax)
                    .AddBackpropagation(errorMetric)
                    .Build()
                ;
                testData.AddTarget(network);

                for (var i = 0; i < 5000; i++) {
                    var trainingError = engine.Train();
                    if (i % 100 == 0)
                        engine.WriteTestResults(errorMetric);
                }
                engine.WriteTestResults(errorMetric);
            }
        }

        static void Main(string[] args)
        {
            //var matrix = lap.Create(3, 3, (i, j) => (i+1) * (j+1));
            //var rot180 = matrix.Rotate180();

            //var xml = matrix.AsIndexable().AsXml;
            //var xml2 = rot180.AsIndexable().AsXml;

            IrisTest();
        }
    }
}