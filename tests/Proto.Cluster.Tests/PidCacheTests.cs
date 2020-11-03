#nullable enable
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Proto.Cluster.IdentityLookup;
using Xunit;

namespace Proto.Cluster.Tests
{
    using ClusterTest.Messages;

    public class DummyIdentityLookup : IIdentityLookup
    {
        private readonly PID _pid;

        public DummyIdentityLookup(PID pid)
        {
            _pid = pid;
        }

        public Task<PID?> GetAsync(string identity, string kind, CancellationToken ct)
        {
            return Task.FromResult(_pid)!;
        }

        public Task SetupAsync(Cluster cluster, string[] kinds, bool isClient)
        {
            return Task.CompletedTask;
        }

        public Task ShutdownAsync()
        {
            return Task.CompletedTask;
        }
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
            await system.Root.StopAsync(deadPid);

            var dummyIdentityLookup = new DummyIdentityLookup(alivePid);
            var pidCache = new PidCache();

            var logger = Log.CreateLogger("dummylog");
            pidCache.TryAdd("kind", "identity", deadPid);
            var requestAsyncStrategy = new DefaultClusterContext(dummyIdentityLookup, pidCache, system.Root, logger);

            var res = await requestAsyncStrategy.RequestAsync<Pong>("identity", "kind", new Ping {Message = "msg"},
                new CancellationTokenSource(6000).Token
            );

            res.Message.Should().Be("msg");
            var foundInCache = pidCache.TryGet("kind", "identity", out var pidInCache);
            foundInCache.Should().BeTrue();
            pidInCache.Should().BeEquivalentTo(alivePid);
        }
    }
}