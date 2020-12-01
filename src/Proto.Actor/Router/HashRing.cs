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
    public class HashRing
    {
        private readonly Func<string, uint> _hash;
        private readonly List<Tuple<uint, string>> _ring;

        public HashRing(IEnumerable<string> nodes, Func<string, uint> hash, int replicaCount)
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
                                    hashKey = i + n,
                                    node = n
                                }
                            )
                )
                .Select(a => Tuple.Create(_hash(a.hashKey), a.node))
                .OrderBy(t => t.Item1)
                .ToList();
        }

        public string GetNode(string key) => (_ring.FirstOrDefault(t => t.Item1 > _hash(key)) ?? _ring.First()).Item2;
    }
}