// -----------------------------------------------------------------------
// <copyright file="HashedConcurrentDictionary.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Generic;
using System.Threading;

namespace Proto
{
    class HashedConcurrentDictionary
    {
        private const int HashSize = 1024;
        private readonly Partition[] _partitions = new Partition[HashSize];

        private int _count;
//        public int Count => _partitions.Select(partition => partition.Count).Sum();

        internal HashedConcurrentDictionary()
        {
            for (var i = 0; i < _partitions.Length; i++)
            {
                _partitions[i] = new Partition();
            }
        }

        public int Count => _count;

        private Partition GetPartition(string key)
        {
            var hash = key.GetHashCode() & (0x7FFFFFFF % HashSize);

            var p = _partitions[hash];
            return p;
        }

        public bool TryAdd(string key, Process reff)
        {
            var p = GetPartition(key);

            lock (p)
            {
                if (p.ContainsKey(key)) return false;

                p.Add(key, reff);
                Interlocked.Increment(ref _count);
                return true;
            }
        }

        public bool TryGetValue(string key, out Process aref)
        {
            var p = GetPartition(key);
            lock (p) return p.TryGetValue(key, out aref);
        }

        public void Remove(string key)
        {
            var p = GetPartition(key);

            lock (p)
            {
                if (p.Remove(key))
                    Interlocked.Decrement(ref _count);
            }
        }

        private class Partition : Dictionary<string, Process>
        {
        }
    }
}