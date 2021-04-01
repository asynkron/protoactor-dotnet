#nullable enable
using System.Threading;
using System.Threading.Tasks;
using ClusterTest.Messages;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Proto.Cluster.Identity;
using Proto.Cluster.Metrics;
using Xunit;

namespace Proto.Cluster.Tests
{
    public class DummyIdentityLookup : IIdentityLookup
    {
        private readonly PID _pid;

        public DummyIdentityLookup(PID pid) => _pid = pid;

        public Task<PID?> GetAsync(ClusterIdentity clusterIdentity, CancellationToken ct) => Task.FromResult(_pid)!;

        public Task RemovePidAsync(ClusterIdentity clusterIdentity, PID pid, CancellationToken ct) =>
            Task.CompletedTask;

        public Task SetupAsync(Cluster cluster, string[] kinds, bool isClient) => Task.CompletedTask;

        public Task ShutdownAsync() => Task.CompletedTask;
    }

    public class PidCacheTests
    {
        [Fact]
        public async Task PurgesPidCacheOnNullResponse()
        {
            ActorSystem? system = new ActorSystem();
            system.Metrics.RegisterKnownMetrics(new ClusterMetrics(system.Metrics));
            Props? props = Props.FromProducer(() => new EchoActor());
            PID? deadPid = system.Root.SpawnNamed(props, "stopped");
            PID? alivePid = system.Root.SpawnNamed(props, "alive");
            await system.Root.StopAsync(deadPid).ConfigureAwait(false);

            DummyIdentityLookup? dummyIdentityLookup = new DummyIdentityLookup(alivePid);
            PidCache? pidCache = new PidCache();

            ILogger? logger = Log.CreateLogger("dummylog");
            ClusterIdentity? clusterIdentity = new ClusterIdentity {Identity = "identity", Kind = "kind"};
            pidCache.TryAdd(clusterIdentity, deadPid);
            DefaultClusterContext? requestAsyncStrategy = new DefaultClusterContext(dummyIdentityLookup, pidCache,
                logger, new ClusterContextConfig(), system.Shutdown);

            Pong? res = await requestAsyncStrategy.RequestAsync<Pong>(clusterIdentity, new Ping {Message = "msg"},
                system.Root,
                new CancellationTokenSource(6000).Token
            );

            res.Message.Should().Be("msg");
            bool foundInCache = pidCache.TryGet(clusterIdentity, out var pidInCache);
            foundInCache.Should().BeTrue();
            pidInCache.Should().BeEquivalentTo(alivePid);
        }
    }
}
