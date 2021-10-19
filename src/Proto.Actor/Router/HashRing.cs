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
    public class HashRing<T>
    {
        private readonly Func<string, uint> _hash;
        private readonly List<Tuple<uint, T>> _ring;

        public HashRing(IEnumerable<T> nodes, Func<T,string> getKey, Func<string, uint> hash, int replicaCount)
        {
            _hash = hash;

            _ring = nodes
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
                .ToList();
        }

        public T GetNode(string key)
        {
            var hash = _hash(key);
            return (_ring.Find(t => t.Item1 >= hash) ?? _ring[0]).Item2;
        }
    }
}