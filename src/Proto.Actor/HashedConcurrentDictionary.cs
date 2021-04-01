// -----------------------------------------------------------------------
// <copyright file="HashedConcurrentDictionary.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;

namespace Proto
{
    internal class HashedConcurrentDictionary : HashedConcurrentDictionary<string, Process>
    {
    }

    internal class HashedConcurrentDictionary<TKey, TValue>
    {
        private const int HashSize = 1024;
        private readonly Dictionary<TKey, TValue>[] _partitions = new Dictionary<TKey, TValue>[HashSize];

        internal HashedConcurrentDictionary()
        {
            for (int i = 0; i < _partitions.Length; i++)
            {
                _partitions[i] = new Dictionary<TKey, TValue>();
            }
        }

        public int Count { get; private set; }

        private Dictionary<TKey, TValue> GetPartition(TKey key)
        {
            int hash = key.GetHashCode() & (0x7FFFFFFF % HashSize);

            Dictionary<TKey, TValue>? p = _partitions[hash];
            return p;
        }

        public bool TryAdd(TKey key, TValue value)
        {
            Dictionary<TKey, TValue>? p = GetPartition(key);

            lock (p)
            {
                if (p.ContainsKey(key))
                {
                    return false;
                }

                p.Add(key, value);
                Count++;
                return true;
            }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            Dictionary<TKey, TValue>? p = GetPartition(key);
            lock (p)
            {
                return p.TryGetValue(key, out value);
            }
        }

        public bool Remove(TKey key)
        {
            Dictionary<TKey, TValue>? p = GetPartition(key);

            lock (p)
            {
                if (p.Remove(key))
                {
                    Count--;
                    return true;
                }

                return false;
            }
        }

        public bool RemoveByVal(TKey key, TValue val)
        {
            Dictionary<TKey, TValue>? p = GetPartition(key);

            lock (p)
            {
                if (p.TryGetValue(key, out TValue existing) && val.Equals(existing))
                {
                    p.Remove(key);
                    Count--;
                    return true;
                }
            }

            return false;
        }
    }
}
