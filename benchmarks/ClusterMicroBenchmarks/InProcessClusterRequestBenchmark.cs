// -----------------------------------------------------------------------
// <copyright file="PidCacheBenchmark.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
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
    public class InProcessClusterRequestBenchmark
    {
        private Cluster _cluster;
        private const string Kind = "echo";

        private ClusterIdentity _id;
        private PID pid;

        [Params(false)]
        public bool LocalAffinity { get; set; }

        [Params(true, false)]
        public bool SharedFutures { get; set; }

        [Params(false)]
        public bool RequestDeduplication { get; set; }

        [GlobalSetup]
        public async Task Setup()
        {
            var echoProps = Props.FromFunc(ctx => {
                    if (ctx.Sender is not null) ctx.Respond(ctx.Message!);
                    return Task.CompletedTask;
                }
            );

            if (RequestDeduplication)
            {
                echoProps = echoProps.WithClusterRequestDeduplication(TimeSpan.FromSeconds(30));
            }

            var echoKind = new ClusterKind(Kind, echoProps);

            if (LocalAffinity)
            {
                echoKind.WithLocalAffinityRelocationStrategy();
            }

            var sys = new ActorSystem(new ActorSystemConfig
                    {
                        SharedFutures = SharedFutures
                    }
                )
                .WithRemote(GrpcNetRemoteConfig.BindToLocalhost(9090))
                .WithCluster(ClusterConfig().WithClusterKind(echoKind));

            pid = sys.Root.SpawnNamed(echoProps, "thing");

            _cluster = sys.Cluster();
            await _cluster.StartMemberAsync();

            _id = ClusterIdentity.Create("1", Kind);
            await _cluster.RequestAsync<int>(_id.Identity, _id.Kind, 1, CancellationToken.None);
        }

        private static ClusterConfig ClusterConfig() => Proto.Cluster.ClusterConfig.Setup("testcluster",
            new TestProvider(new TestProviderOptions(), new InMemAgent()),
            new PartitionIdentityLookup()
        );

        [GlobalCleanup]
        public Task Cleanup() => _cluster.ShutdownAsync();

        [Benchmark]
        public Task RequestAsync() => _cluster.System.Root.RequestAsync<object>(pid, 1, CancellationToken.None);

        [Benchmark]
        public Task ClusterRequestAsync() => _cluster.RequestAsync<int>(_id.Identity, _id.Kind, 1, CancellationToken.None);

        [Benchmark]
        public Task ClusterIdentityRequestAsync() => _cluster.RequestAsync<int>(_id, 1, CancellationToken.None);
    }
}