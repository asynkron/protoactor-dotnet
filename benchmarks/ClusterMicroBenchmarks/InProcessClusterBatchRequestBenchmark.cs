// -----------------------------------------------------------------------
// <copyright file="InProcessClusterBatchRequestBenchmark.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
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
    public class InProcessClusterBatchRequestBenchmark
    {
        private const string Kind = "echo";

        [Params(2000)]
        public int BatchSize { get; set; }

        [Params(10000)]
        public int Identities { get; set; }

        [Params(true)]
        public bool ExperimentalContext { get; set; }

        [Params(true, false)]
        public bool PassCancellationToken { get; set; }

        private Cluster _cluster;
        private ClusterIdentity[] _ids;

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
            _ids = new ClusterIdentity[Identities];

            for (int i = 0; i < _ids.Length; i++)
            {
                _ids[i] = ClusterIdentity.Create(i.ToString(), Kind);
            }

            // Activate identities
            foreach (var clusterIdentity in _ids)
            {
                await _cluster.RequestAsync<int>(clusterIdentity, 1, CancellationToken.None);
            }
        }

        private ClusterConfig ClusterConfig()
        {
            var config = Proto.Cluster.ClusterConfig.Setup("testcluster",
                new TestProvider(new TestProviderOptions(), new InMemAgent()),
                new PartitionIdentityLookup()
            );

            if (ExperimentalContext)
            {
                config = config.WithClusterContextProducer(cluster => new ExperimentalClusterContext(cluster));
            }

            return config;
        }

        [GlobalCleanup]
        public Task Cleanup() => _cluster.ShutdownAsync();

        // [Benchmark]
        // public async Task ClusterRequestAsync()
        // {
        //     var ct = PassCancellationToken ? CancellationTokens.FromSeconds(10) : CancellationToken.None;
        //
        //     var tasks = new Task[BatchSize];
        //
        //     for (var i = 0; i < BatchSize; i++)
        //     {
        //         var id = _ids[i];
        //         tasks[i] = _cluster.RequestAsync<int>(id.Identity, id.Kind, i, ct);
        //     }
        //
        //     await Task.WhenAll(tasks);
        // }
        //
        // [Benchmark]
        // public async Task ClusterRequestBatchAsync()
        // {
        //     var ct = PassCancellationToken ? CancellationTokens.FromSeconds(10) : CancellationToken.None;
        //     using var batch = _cluster.System.Root.Batch(BatchSize, ct);
        //     var tasks = new Task[BatchSize];
        //
        //     for (var i = 0; i < BatchSize; i++)
        //     {
        //         var id = _ids[i];
        //         tasks[i] = _cluster.RequestAsync<int>(id.Identity, id.Kind, i, batch, ct);
        //     }
        //
        //     await Task.WhenAll(tasks);
        // }

        [Benchmark]
        public async Task ClusterRequestAsyncBatchReuseIdentity()
        {
            var ct = PassCancellationToken ? CancellationTokens.FromSeconds(10) : CancellationToken.None;
            using var batch = _cluster.System.Root.CreateBatchContext(BatchSize, ct);
            var tasks = new Task[BatchSize];

            for (var i = 0; i < BatchSize; i++)
            {
                var id = _ids[i];
                tasks[i] = _cluster.RequestAsync<int>(id, i, batch, ct);
            }

            await Task.WhenAll(tasks);
        }
        

        [Benchmark]
        public async Task ClusterRequestAsyncReuseIdentity()
        {
            var ct = PassCancellationToken ? CancellationTokens.FromSeconds(10) : CancellationToken.None;

            var tasks = new Task[BatchSize];

            for (var i = 0; i < BatchSize; i++)
            {
                var id = _ids[i];
                tasks[i] = _cluster.RequestAsync<int>(id, i, ct);
            }

            await Task.WhenAll(tasks);
        }
    }
}