﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using BrightData.Helper;

namespace BrightData.Buffers
{
    public abstract class HybridBufferBase<T> : IAutoGrowBuffer<T>
    {
        private readonly TempStreamManager _tempStreams;
        private readonly int _bufferSize;
        readonly ConcurrentBag<T> _buffer = new ConcurrentBag<T>();
        private bool _hasWrittenToStream = false;
        protected readonly IBrightDataContext _context;
        private readonly uint _index;
        private uint _size;

        protected HybridBufferBase(IBrightDataContext context, uint index, TempStreamManager tempStreams, int bufferSize = 32768)
        {
            _context = context;
            _index = index;
            _tempStreams = tempStreams;
            _bufferSize = bufferSize;
        }

        public void WriteTo(BinaryWriter writer)
        {
            if (_hasWrittenToStream) {
                var stream = _tempStreams.Get(_index);
                lock (stream) {
                    stream.Seek(0, SeekOrigin.Begin);
                    stream.CopyTo(writer.BaseStream);
                }
            }

            _Write(_buffer, writer.BaseStream);
        }

        protected abstract void _Write(ConcurrentBag<T> items, Stream stream);
        protected abstract IEnumerable<T> _Read(Stream stream);

        public IEnumerable<T> EnumerateTyped()
        {
            if (_hasWrittenToStream) {
                var stream = _tempStreams.Get(_index);
                lock (stream) {
                    stream.Seek(0, SeekOrigin.Begin);
                    foreach (var item in _Read(stream))
                        yield return item;
                }
            }

            foreach (var item in _buffer)
                yield return item;
        }
        public IEnumerable<object> Enumerate()
        {
            foreach (var item in EnumerateTyped())
                yield return item;
        }

        public uint Size => _size + (uint)_buffer.Count;
        void IAutoGrowBuffer.Add(object obj) => Add((T)obj);
        public void Add(T typedObject)
        {
            if (_buffer.Count == _bufferSize) {
                var stream = _tempStreams.Get(_index);
                lock (stream) {
                    if (_buffer.Count == _bufferSize) {
                        _Write(_buffer, stream);
                        _size += (uint)_buffer.Count;
                        _buffer.Clear();
                    }
                }
            }

            _buffer.Add(typedObject);
        }
    }
}