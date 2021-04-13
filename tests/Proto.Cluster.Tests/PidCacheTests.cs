#nullable enable
using System.Threading;
using System.Threading.Tasks;
using ClusterTest.Messages;
using FluentAssertions;
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
            system.Metrics.Register(new ClusterMetrics(system.Metrics));
            var props = Props.FromProducer(() => new EchoActor());
            var deadPid = system.Root.SpawnNamed(props, "stopped");
            var alivePid = system.Root.SpawnNamed(props, "alive");
            await system.Root.StopAsync(deadPid).ConfigureAwait(false);

            var dummyIdentityLookup = new DummyIdentityLookup(alivePid);
            var pidCache = new PidCache();

            var logger = Log.CreateLogger("dummylog");
            var clusterIdentity = new ClusterIdentity {Identity = "identity", Kind = "kind"};
            pidCache.TryAdd(clusterIdentity, deadPid);
            var requestAsyncStrategy = new DefaultClusterContext(dummyIdentityLookup, pidCache, new ClusterContextConfig(), system.Shutdown);

            var res = await requestAsyncStrategy.RequestAsync<Pong>(clusterIdentity, new Ping {Message = "msg"}, system.Root,
                new CancellationTokenSource(6000).Token
            );

            res!.Message.Should().Be("msg");
            var foundInCache = pidCache.TryGet(clusterIdentity, out var pidInCache);
            foundInCache.Should().BeTrue();
            pidInCache.Should().BeEquivalentTo(alivePid);
        }
    }
}