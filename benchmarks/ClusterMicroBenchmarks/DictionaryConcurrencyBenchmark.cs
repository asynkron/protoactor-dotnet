// -----------------------------------------------------------------------
// <copyright file="PidCacheBenchmark.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Proto;
using Proto.Cluster;

namespace ClusterMicroBenchmarks
{
    [MemoryDiagnoser, InProcess]
    public class DictionaryConcurrencyBenchmark
    {
        [Params(100_000, 10_000)]
        public int Identities { get; set; }

        [Params(2_000_000, 100_000)]
        public int Iterations { get; set; }

        [Params(32)]
        public int Threads { get; set; }

        [Params(.2)]
        public double WriteFactor { get; set; }

        [Params(TestCandidate.ConcurrentDictionary, TestCandidate.HashedDictionary, TestCandidate.HashedRwDictionary)]
        public TestCandidate Candidate { get; set; }

        private ClusterIdentity[] Ids { get; set; }

        private Action<ClusterIdentity, PID> Write;
        private Action<ClusterIdentity> Read;
        private object Sut;

        [GlobalSetup]
        public void Setup()
        {
            Ids = Enumerable.Range(0, Identities).Select(i =>
                    ClusterIdentity.Create(Guid.NewGuid().ToString("N"), i % 3 == 0 ? "kind1" : "kind2")
                )
                .ToArray();
        }

        [IterationSetup]
        public void SetupIteration()
        {
            switch (Candidate)
            {
                case TestCandidate.ConcurrentDictionary:
                    SetupConcurrentDict();
                    break;
                case TestCandidate.HashedDictionary:
                    SetupHashedDict();
                    break;
                case TestCandidate.HashedRwDictionary:
                    SetupHashedRwDict();
                    break;
                default:
                    throw new ArgumentException("Unhandled candidate: " + Candidate);
            }
        }

        private void SetupConcurrentDict()
        {
            var dict = new ConcurrentDictionary<ClusterIdentity, PID>();
            Write = (identity, pid) => dict.TryAdd(identity, pid);
            Read = identity => dict.TryGetValue(identity, out _);
            Sut = dict;
        }

        private void SetupHashedDict()
        {
            var dict = new HashedConcurrentDictionary<ClusterIdentity, PID>();
            Write = (identity, pid) => dict.TryAdd(identity, pid);
            Read = identity => dict.TryGetValue(identity, out _);
            Sut = dict;
        }

        private void SetupHashedRwDict()
        {
            var dict = new HashedRwConcurrentDictionary<ClusterIdentity, PID>();
            Write = (identity, pid) => dict.TryAdd(identity, pid);
            Read = identity => dict.TryGetValue(identity, out _);
            Sut = dict;
        }

        [IterationCleanup]
        public void CleanupIteration()
        {
            if (Sut is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        [Benchmark]
        public Task ConcurrencyBenchmark() => Task.WhenAll(Enumerable.Range(0, Threads).Select(_ => Task.Run(RunThread)));

        void RunThread()
        {
            var rnd = new Random();

            for (int i = 0; i < Iterations; i++)
            {
                var id = Ids[rnd.Next(0, Ids.Length)];

                if (WriteFactor > rnd.NextDouble())
                {
                    var pid = new PID();
                    Write(id, pid);
                }
                else
                {
                    Read(id);
                }
            }
        }

        public enum TestCandidate
        {
            ConcurrentDictionary,
            HashedDictionary,
            HashedRwDictionary
        }
    }
}