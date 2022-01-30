// -----------------------------------------------------------------------
// <copyright file="PidCacheBenchmark.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Partition;
using Proto.Cluster.Testing;
using Proto.Remote.GrpcNet;

namespace ClusterMicroBenchmarks
{
    [MemoryDiagnoser, InProcess]
    public class ConcurrentSpawnBenchmark
    {
        private Cluster _cluster;
        private const string Kind = "echo";

        [Params(IdentityLookup.Partition)]
        public IdentityLookup IdentityProvider { get; set; }

        [Params(10_000, 20_000)]
        public int ConcurrentSpawns { get; set; }

        [GlobalSetup]
        public async Task Setup()
        {
            var echoProps = Props.FromFunc(ctx => {
                    if (ctx.Sender is not null) ctx.Respond(ctx.Message!);
                    return Task.CompletedTask;
                }
            );

            var echoKind = new ClusterKind(Kind, echoProps);

            var sys = new ActorSystem(new ActorSystemConfig())
                .WithRemote(GrpcNetRemoteConfig.BindToLocalhost(9090))
                .WithCluster(ClusterConfig().WithClusterKind(echoKind));

            _cluster = sys.Cluster();
            await _cluster.StartMemberAsync();
        }

        private ClusterConfig ClusterConfig() => Proto.Cluster.ClusterConfig.Setup("test-cluster",
            new TestProvider(new TestProviderOptions(), new InMemAgent()),
            GetIdentityLookup()
        );

        private PartitionIdentityLookup GetIdentityLookup()
        {
            switch (IdentityProvider)
            {
                case IdentityLookup.Partition:
                    return new PartitionIdentityLookup();
                default:
                    throw new NotImplementedException();
            }
        }

        [GlobalCleanup]
        public Task Cleanup() => _cluster.ShutdownAsync();

        [Benchmark]
        public async Task SpawnIdentities()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var tasks = new Task<PID>[ConcurrentSpawns];

            for (var i = 0; i < ConcurrentSpawns; i++)
            {
                tasks[i] = _cluster.GetAsync(i.ToString(), Kind, cts.Token);
            }

            var pids = await Task.WhenAll(tasks);

            for (var i = 0; i < ConcurrentSpawns; i++)
            {
                if (pids[i] is null) throw new Exception("Failed to return id " + i);
            }
        }

        [IterationCleanup]
        public void RemoveActivations()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var tasks = new Task[ConcurrentSpawns];

            for (var i = 0; i < ConcurrentSpawns; i++)
            {
                var id = ClusterIdentity.Create(i.ToString(), Kind);
                tasks[i] = _cluster.RequestAsync<Terminated>(id, PoisonPill.Instance, cts.Token);
            }

            Task.WhenAll(tasks).GetAwaiter().GetResult();
        }

        public enum IdentityLookup
        {
            Partition,
            Redis,
            MongoDb
        }
    }
}