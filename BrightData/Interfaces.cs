﻿using System;
using System.Collections.Generic;
using System.IO;

namespace BrightData
{
    public interface IHaveIndices
    {
        IEnumerable<uint> Indices { get; }
    }

    public interface ICanWriteToBinaryWriter
    {
        void WriteTo(BinaryWriter writer);
    }

    public interface IMetaData : ICanWriteToBinaryWriter
    {
        object Get(string name);
        T Get<T>(string name, T valueIfMissing = default) where T : IConvertible;
        T Set<T>(string name, T value) where T : IConvertible;
        string AsXml { get; }
        void CopyTo(IMetaData metadata, params string[] keys);
        void CopyAllExcept(IMetaData metadata, params string[] keys);
        void ReadFrom(BinaryReader reader);
    }

    public interface IHaveMetaData
    {
        IMetaData MetaData { get; }
    }

    public interface IReferenceCountedMemory
    {
        uint Size { get; }
        int AddRef();
        int Release();
        long AllocationIndex { get; }
        bool IsValid { get; }
    }

    public interface ITensor : IDisposable
    {
        uint[] Shape { get; }
    }

    public interface ITensor<T> : ITensor
    {
        ITensorSegment<T> GetData();
    }

    public interface IMemoryDeallocator
    {
        void Free();
    }

    public interface ITensorBlock<T> : IReferenceCountedMemory
    {
        string TensorType { get; }
        ITensorSegment<T> GetSegment();
        IBrightDataContext Context { get; }
        void CopyFrom(T[] array);
    }

    public interface IDataReader
    {
        T Read<T>(BinaryReader reader);
        T[] ReadArray<T>(BinaryReader reader);
        //uint Read(BinaryReader reader);
    }

    public interface ITensorPool
    {
        ITensorBlock<T> Get<T>(uint size);
        void Add<T>(ITensorBlock<T> block);
        long MaxCacheSize { get; }
        long AllocationSize { get; }
        long CacheSize { get; }
    }

    public interface IDisposableLayers
    {
        void Add(IDisposable disposable);
        IDisposable Push();
        void Pop();
    }

    public interface IBrightDataContext : IDisposable
    {
        ITensorPool TensorPool { get; }
        IDisposableLayers MemoryLayer { get; }
        IDataReader DataReader { get; }
        INumericComputation<T> GetComputation<T>();
    }

    public interface ITensorSegment<T> : IReferenceCountedMemory, IDisposable
    {
        IBrightDataContext Context { get; }
        ITensorBlock<T> GetBlock(ITensorPool pool);
        T this[uint index] { get; set; }
        T this[long index] { get; set; }
        T[] ToArray();
        IEnumerable<T> Values { get; }
        void CopyFrom(T[] array);
        void Initialize(Func<uint, T> initializer);
    }

    public interface ITensorAllocator
    {
        ITensorBlock<T> Create<T>(IBrightDataContext context, uint size);
    }

    public interface IHaveTensorSegment<T>
    {
        ITensorSegment<T> Data { get; }
    }

    public interface INumericComputation<T>
    {
        ITensorSegment<T> Add(ITensorSegment<T> tensor1, ITensorSegment<T> tensor2);
        void AddInPlace(ITensorSegment<T> target, ITensorSegment<T> other);
        ITensorSegment<T> Subtract(ITensorSegment<T> tensor1, ITensorSegment<T> tensor2);
        void SubtractInPlace(ITensorSegment<T> target, ITensorSegment<T> other);
        ITensorSegment<T> Multiply(ITensorSegment<T> tensor1, ITensorSegment<T> tensor2);
        void MultiplyInPlace(ITensorSegment<T> target, ITensorSegment<T> other);
        ITensorSegment<T> Divide(ITensorSegment<T> tensor1, ITensorSegment<T> tensor2);
        void DivideInPlace(ITensorSegment<T> target, ITensorSegment<T> other);
        T SumIndexedProducts(uint size, Func<uint, T> p1, Func<uint, T> p2);
    }
}