﻿using BrightWire.ExecutionGraph.Helper;
using BrightWire.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BrightWire.ExecutionGraph.DataSource
{
    /// <summary>
    /// Feeds data to the execution graph
    /// </summary>
    class VectorDataSource : IDataSource
    {
        readonly int _inputSize, _outputSize;
        readonly IReadOnlyList<FloatVector> _data;
        readonly ILinearAlgebraProvider _lap;

        public VectorDataSource(ILinearAlgebraProvider lap, IReadOnlyList<FloatVector> data)
        {
            _lap = lap;
            _data = data;

            var first = data.First();
            _inputSize = first.Size;
            _outputSize = -1;
        }

        public bool IsSequential => false;
        public int InputSize => _inputSize;
        public int OutputSize => _outputSize;
        public int RowCount => _data.Count;

        public IMiniBatch Get(IExecutionContext executionContext, IReadOnlyList<int> rows)
        {
            var data = rows.Select(i => _data[i]).ToList();
            var input = _lap.CreateMatrix(data.Count, InputSize, (x, y) => data[x].Data[y]);
            return new MiniBatch(rows, this, new MatrixGraphData(input), null);
        }

        public IReadOnlyList<IReadOnlyList<int>> GetBuckets()
        {
            return new[] {
                Enumerable.Range(0, _data.Count).ToList()
            };
        }

        public IDataSource CloneWith(IDataTable dataTable)
        {
            throw new NotImplementedException();
        }

        public void OnBatchProcessed(IContext context)
        {
            // nop
        }
    }
}