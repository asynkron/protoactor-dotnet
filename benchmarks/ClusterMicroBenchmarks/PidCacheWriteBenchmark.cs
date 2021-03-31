// -----------------------------------------------------------------------
// <copyright file="PidCacheBenchmark.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Proto;
using Proto.Cluster;

namespace ClusterMicroBenchmarks
{
    [MemoryDiagnoser, InProcess]
    public class PidCacheWriteBenchmark
    {
        private PidCache _pidCache;

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
            _pidCache = new PidCache();
            Random = new Random(999);
        }

        [Benchmark]
        public void WriteBenchmark()
        {
            foreach (var clusterIdentity in Ids)
            {
                _pidCache.TryAdd(clusterIdentity, new PID());
            }
        }
    }
}