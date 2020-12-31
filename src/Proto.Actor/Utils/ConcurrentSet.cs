// -----------------------------------------------------------------------
// <copyright file="ConcurrentSet.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Concurrent;
using System.Linq;
using JetBrains.Annotations;

namespace Proto.Utils
{
    [PublicAPI]
    public class ConcurrentSet<T>
    {
        private readonly ConcurrentDictionary<T, byte> _inner = new();

        public bool Contains(T key) => _inner.ContainsKey(key);

        public void Add(T key) => _inner.TryAdd(key, 1);

        public bool TryAdd(T key) => _inner.TryAdd(key, 1);

        public void Remove(T key) => _inner.TryRemove(key, out _);

        public T[] ToArray() => _inner.Keys.ToArray();
    }
}