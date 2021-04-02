// -----------------------------------------------------------------------
// <copyright file="PidCacheBenchmark.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Proto;
using Proto.Cluster;

namespace ClusterMicroBenchmarks
{
    [MemoryDiagnoser, InProcess]
    public class DictionaryWriteBenchmarks
    {
        private ConcurrentDictionary<ClusterIdentity, PID> _concurrentDictionary;
        private HashedConcurrentDictionary<ClusterIdentity, PID> _hashedConcurrentDictionary;

        [Params(1_000_000, 100_000, 10_000, 1000)]
        public int Identities { get; set; }

        private ClusterIdentity[] Ids { get; set; }
        private Random Random { get; set; }

        [GlobalSetup]
        public void Setup() => Ids = Enumerable.Range(0, Identities).Select(i =>
                ClusterIdentity.Create(Guid.NewGuid().ToString("N"), i % 3 == 0 ? "kind1" : "kind2")
            )
            .ToArray();

        [IterationSetup]
        public void IterationSetup()
        {
            _concurrentDictionary = new();
            _hashedConcurrentDictionary = new();
            Random = new Random();
        }

        [Benchmark]
        public void ConcurrentDictionaryWriteBenchmark()
        {
            foreach (var clusterIdentity in Ids)
            {
                _concurrentDictionary.TryAdd(clusterIdentity, new PID());
            }
        }
        
        [Benchmark]
        public void HashedConcurrentDictionaryWriteBenchmark()
        {
            foreach (var clusterIdentity in Ids)
            {
                _hashedConcurrentDictionary.TryAdd(clusterIdentity, new PID());
            }
        }
    }
}