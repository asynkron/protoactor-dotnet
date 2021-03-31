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
    [InProcess]
    public class PidCacheReadBenchmark
    {
        private PidCache _pidCache;

        [Params(1_000_000, 100_000, 10_000, 1000)]
        public int Identities { get; set; }

        private ClusterIdentity[] Ids { get; set; }
        private Random Random { get; set; } = new Random(999);

        [GlobalSetup]
        public void Setup()
        {
            Ids = Enumerable.Range(0, Identities).Select(i =>
                    ClusterIdentity.Create(Guid.NewGuid().ToString("N"), i % 3 == 0 ? "kind1" : "kind2")
                )
                .ToArray();
            _pidCache = new PidCache();

            foreach (var clusterIdentity in Ids)
            {
                _pidCache.TryAdd(clusterIdentity, new PID());
            }
        }

        [Benchmark]
        public void ReadBenchmark()
        {
            var id = Ids[Random.Next(0, Ids.Length)];
            _pidCache.TryGet(id, out _);
        }
    }
}