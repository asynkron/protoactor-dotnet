// -----------------------------------------------------------------------
// <copyright file="HashedConcurrentDictionary.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;

namespace Proto
{
    class HashedRwConcurrentDictionary : HashedRwConcurrentDictionary<string, Process>
    {
    }

    class HashedRwConcurrentDictionary<TKey, TValue> : IDisposable
    {
        private const int HashSize = 1024;

        private readonly (Dictionary<TKey, TValue> partitionDictionary, ReaderWriterLockSlim partitionLock)[] _partitions =
            new (Dictionary<TKey, TValue> partitionDictionary, ReaderWriterLockSlim partitionLock)[HashSize];

        private int _count;

        internal HashedRwConcurrentDictionary()
        {
            for (var i = 0; i < _partitions.Length; i++)
            {
                _partitions[i] = (new Dictionary<TKey, TValue>(), new ReaderWriterLockSlim());
            }
        }

        public int Count => _count;

        private (Dictionary<TKey, TValue>, ReaderWriterLockSlim) GetPartition(TKey key)
        {
            var hash = key.GetHashCode() & (0x7FFFFFFF % HashSize);

            return _partitions[hash];
        }

        public bool TryAdd(TKey key, TValue value) => WithWriteLock(key, p => {
                if (p.ContainsKey(key)) return false;

                p.Add(key, value);
                Interlocked.Increment(ref _count);
                return true;
            }
        );

        public bool TryGetValue(TKey key, out TValue value)
        {
            var (p, l) = GetPartition(key);

            l.TryEnterReadLock(-1);

            try
            {
                return p.TryGetValue(key, out value);
            }
            finally
            {
                l.ExitReadLock();
            }
        }

        public bool Remove(TKey key) => WithWriteLock(key, p => {
                if (p.Remove(key))
                {
                    Interlocked.Decrement(ref _count);
                    return true;
                }

                return false;
            }
        );

        public bool RemoveByVal(TKey key, TValue val) => WithWriteLock(key, p => {
                if (p.TryGetValue(key, out var existing) && val.Equals(existing))
                {
                    p.Remove(key);
                    Interlocked.Decrement(ref _count);
                    return true;
                }

                return false;
            }
        );

        private bool WithWriteLock(TKey key, Func<Dictionary<TKey, TValue>, bool> update)
        {
            var (p, l) = GetPartition(key);

            l.TryEnterWriteLock(-1);

            try
            {
                return update(p);
            }
            finally
            {
                l.ExitWriteLock();
            }
        }

        public void Dispose()
        {
            foreach (var (_, readerWriterLockSlim) in _partitions)
            {
                readerWriterLockSlim.Dispose();
            }
        }
    }
}