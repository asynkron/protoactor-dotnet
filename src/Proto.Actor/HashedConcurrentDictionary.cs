// -----------------------------------------------------------------------
//   <copyright file="HashedConcurrentDictionary.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Proto
{
    internal class HashedConcurrentDictionary
    {
        private const int HashSize = 1024;
        private readonly Partition[] _partitions = new Partition[HashSize];

        internal HashedConcurrentDictionary()
        {
            for (var i = 0; i < _partitions.Length; i++)
            {
                _partitions[i] = new Partition();
            }
        }

        private Partition GetPartition(string key)
        {
            var hash = Math.Abs(key.GetHashCode()) % HashSize;
            var p = _partitions[hash];
            return p;
        }

        public bool TryAdd(string key, Process reff)
        {
            var p = GetPartition(key);
            lock (p)
            {
                if (p.ContainsKey(key))
                {
                    return false;
                }

                p.Add(key, reff);
                return true;
            }
        }

        public bool TryGetValue(string key, out Process aref)
        {
            var p = GetPartition(key);
            lock (p)
            {
                return p.TryGetValue(key, out aref);
            }
        }

        public void Remove(string key)
        {
            var p = GetPartition(key);
            lock (p)
            {
                p.Remove(key);
            }
        }

        private class Partition : Dictionary<string, Process>
        {
        }
    }
}