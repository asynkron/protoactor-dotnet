#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using ClusterTest.Messages;
using FluentAssertions;
using Proto.Cluster.Identity;
using Proto.Cluster.Metrics;
using Proto.Cluster.Partition;
using Proto.Cluster.Testing;
using Proto.Remote.GrpcCore;
using Xunit;

namespace Proto.Cluster.Tests
{
    public class DummyIdentityLookup : IIdentityLookup
    {
        private readonly PID _pid;

        public DummyIdentityLookup(PID pid) => _pid = pid;

        public Task<PID?> GetAsync(ClusterIdentity clusterIdentity, CancellationToken ct) => Task.FromResult(_pid)!;

        public Task RemovePidAsync(ClusterIdentity clusterIdentity, PID pid, CancellationToken ct) => Task.CompletedTask;

        public Task SetupAsync(Cluster cluster, string[] kinds, bool isClient) => Task.CompletedTask;

        public Task ShutdownAsync() => Task.CompletedTask;
    }

    public class PidCacheTests
    {
        [Fact]
        public async Task PurgesPidCacheOnNullResponse()
        {
            var system = new ActorSystem();
            var props = Props.FromProducer(() => new EchoActor());
            var deadPid = system.Root.SpawnNamed(props, "stopped");
            var alivePid = system.Root.SpawnNamed(props, "alive");
            await system.Root.StopAsync(deadPid).ConfigureAwait(false);

            var dummyIdentityLookup = new DummyIdentityLookup(alivePid);
            var pidCache = new PidCache();

            var logger = Log.CreateLogger("dummylog");
            var clusterIdentity = new ClusterIdentity {Identity = "identity", Kind = "kind"};
            pidCache.TryAdd(clusterIdentity, deadPid);
            var requestAsyncStrategy = new DefaultClusterContext(system, dummyIdentityLookup, pidCache, new ClusterContextConfig(), system.Shutdown);

            var res = await requestAsyncStrategy.RequestAsync<Pong>(clusterIdentity, new Ping {Message = "msg"}, system.Root,
                new CancellationTokenSource(6000).Token
            );

            res!.Message.Should().Be("msg");
            var foundInCache = pidCache.TryGet(clusterIdentity, out var pidInCache);
            foundInCache.Should().BeTrue();
            pidInCache.Should().BeEquivalentTo(alivePid);
        }

        [Fact]
        public async Task PurgesPidCacheOnVirtualActorShutdown()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var system = new ActorSystem()
                .WithRemote(GrpcCoreRemoteConfig.BindToLocalhost())
                .WithCluster(GetClusterConfig());

            var cluster = system.Cluster();
            await cluster.StartMemberAsync();

            var identity = ClusterIdentity.Create("", "echo");

            await cluster.RequestAsync<Ack>(identity, new Die(), timeout.Token);

            // Let the system purge the terminated PID,
            await Task.Delay(50);

            cluster.PidCache.TryGet(identity, out _).Should().BeFalse();
        }

        ClusterConfig GetClusterConfig() => ClusterConfig
            .Setup(
                "MyCluster",
                new TestProvider(new TestProviderOptions(), new InMemAgent()),
                new PartitionIdentityLookup()
            )
            .WithClusterKind("echo", Props.FromProducer(() => new EchoActor()));
    }
}