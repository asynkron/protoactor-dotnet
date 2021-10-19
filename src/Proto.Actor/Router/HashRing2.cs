// -----------------------------------------------------------------------
// <copyright file="HashRing.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;

namespace Proto.Router
{
    public class HashRing2<T>
    {
        private readonly Func<string, uint> _hash;
        private readonly uint[] _hashes;
        private readonly T[] _values;

        public HashRing2(IEnumerable<T> nodes, Func<T, string> getKey, Func<string, uint> hash, int replicaCount)
        {
            _hash = hash;

            var ring = nodes
                .SelectMany(
                    n =>
                        Enumerable
                            .Range(0, replicaCount)
                            .Select(
                                i => new
                                {
                                    hashKey = i + getKey(n),
                                    node = n
                                }
                            )
                )
                .Select(a => Tuple.Create(_hash(a.hashKey), a.node))
                .OrderBy(t => t.Item1)
                .ToArray();

            _hashes = ring.Select(it => it.Item1).ToArray();
            _values = ring.Select(it => it.Item2).ToArray();
        }

        public T GetNode(string key)
        {
            var hash = _hash(key);

            var result = Array.BinarySearch(_hashes, hash);

            // If the value matches exactly, picks the next value. If it exactly the last value, pick the first one
            if (result >= 0) return _values[result == _hashes.Length ? 0 : result + 1];

            // Get the next higher value by taking the complement of the result
            var nextIndex = ~result;

            // Return the next higher value if it exists, or the first one
            return _values[nextIndex < _values.Length ? nextIndex : 0];

        }
    }
}