// -----------------------------------------------------------------------
//  <copyright file="HashRing.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Proto.Router
{
    public class HashRing
    {
        private const int ReplicaCount = 100;
        private static readonly HashAlgorithm HashAlgorithm = MD5.Create();
        private readonly List<Tuple<uint, string>> _ring;

        public HashRing(IEnumerable<string> nodes)
        {
            _ring = nodes
                .SelectMany(n => Enumerable.Range(0, ReplicaCount).Select(i => new
                {
                    hashKey = i + n,
                    node = n
                }))
                .Select(a => Tuple.Create(Hash(a.hashKey), a.node))
                .OrderBy(t => t.Item1)
                .ToList();
        }

        public string GetNode(string key)
        {
            return (
                _ring.FirstOrDefault(t => t.Item1 > Hash(key))
                ?? _ring.First()
            ).Item2;
        }

        private static uint Hash(string s)
        {
            var digest = HashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(s));
            var hash = BitConverter.ToUInt32(digest, 0);
            return hash;
        }
    }
}