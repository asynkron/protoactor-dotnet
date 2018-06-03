// -----------------------------------------------------------------------
//   <copyright file="HashRing.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Proto.Router
{
    public static class MD5Hasher
    {
        private static readonly HashAlgorithm HashAlgorithm = MD5.Create();

        public static uint Hash(string hashKey)
        {
            var digest = HashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(hashKey));
            var hash = BitConverter.ToUInt32(digest, 0);
            return hash;
        }
    }

    public class HashRing
    {
        private readonly Func<string, uint> _hash;
        private readonly List<Tuple<uint, string>> _ring;

        public HashRing(IEnumerable<string> nodes, Func<string, uint> hash, int replicaCount)
        {
            _hash = hash;
            _ring = nodes
                .SelectMany(n => Enumerable.Range(0, replicaCount).Select(i => new
                {
                    hashKey = i + n,
                    node = n
                }))
                .Select(a => Tuple.Create(_hash(a.hashKey), a.node))
                .OrderBy(t => t.Item1)
                .ToList();
        }

        public string GetNode(string key)
        {
            return (
                _ring.FirstOrDefault(t => t.Item1 > _hash(key))
                ?? _ring.First()
            ).Item2;
        }
    }
}