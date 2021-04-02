// -----------------------------------------------------------------------
// <copyright file="PidCacheBenchmark.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Proto;
using Proto.Cluster;

namespace ClusterMicroBenchmarks
{
    [InProcess]
    public class DictionaryReadBenchmark
    {
        private ImmutableDictionary<ClusterIdentity,PID> _immutableDictionary;
        private ConcurrentDictionary<ClusterIdentity, PID> _concurrentDictionary;
        private HashedConcurrentDictionary<ClusterIdentity, PID> _hashedConcurrentDictionary;

        [Params(1_000_000, 100_000, 10_000, 1000)]
        public int Identities { get; set; }

        private ClusterIdentity[] Ids { get; set; }
        private Random Random { get; } = new Random();

        [GlobalSetup]
        public void Setup()
        {
            Ids = Enumerable.Range(0, Identities).Select(i =>
                    ClusterIdentity.Create(Guid.NewGuid().ToString("N"), i % 3 == 0 ? "kind1" : "kind2")
                )
                .ToArray();
            _concurrentDictionary = new();
            _hashedConcurrentDictionary = new();

            foreach (var clusterIdentity in Ids)
            {
                var value = new PID();
                _concurrentDictionary.TryAdd(clusterIdentity, value);
                _hashedConcurrentDictionary.TryAdd(clusterIdentity, value);
            }

            _immutableDictionary = _concurrentDictionary.ToImmutableDictionary();
        }

        [Benchmark]
        public void ImmutableDictReadBenchmark()
        {
            var id = Ids[Random.Next(0, Ids.Length)];
            _immutableDictionary.TryGetValue(id, out _);
        }
        
        [Benchmark]
        public void ConcurrentDictReadBenchmark()
        {
            var id = Ids[Random.Next(0, Ids.Length)];
            _concurrentDictionary.TryGetValue(id, out _);
        }
        
        [Benchmark]
        public void HashedConcurrentDictReadBenchmark()
        {
            var id = Ids[Random.Next(0, Ids.Length)];
            _hashedConcurrentDictionary.TryGetValue(id, out _);
        }
    }
}