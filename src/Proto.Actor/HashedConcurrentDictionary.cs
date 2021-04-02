// -----------------------------------------------------------------------
// <copyright file="HashedConcurrentDictionary.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Generic;
using System.Threading;

namespace Proto
{
    class HashedConcurrentDictionary : HashedConcurrentDictionary<string, Process>
    {
       
    }
    
    class HashedConcurrentDictionary<TKey, TValue>
    {
        //power of two
        private const int HashSize = 1024;
        private const int HashMask = HashSize-1;
        private readonly Dictionary<TKey,TValue>[] _partitions = new Dictionary<TKey,TValue>[HashSize];

        private int _count;

        internal HashedConcurrentDictionary()
        {
            for (var i = 0; i < _partitions.Length; i++)
            {
                _partitions[i] = new Dictionary<TKey,TValue>();
            }
        }

        public int Count => _count;

        private Dictionary<TKey,TValue> GetPartition(TKey key)
        {
            var hash = key.GetHashCode() & HashMask;

            var p = _partitions[hash];
            return p;
        }

        public bool TryAdd(TKey key, TValue value)
        {
            var p = GetPartition(key);

            lock (p)
            {
                if (p.ContainsKey(key)) return false;

                p.Add(key, value);
                Interlocked.Increment(ref _count);
                return true;
            }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            var p = GetPartition(key);
            lock (p) return p.TryGetValue(key, out value);
        }

        public void Remove(TKey key)
        {
            var p = GetPartition(key);

            lock (p)
            {
                if (p.Remove(key))
                {
                    Interlocked.Decrement(ref _count);
                }
            }
        }
    }
}