#nullable enable
using System.Threading;
using System.Threading.Tasks;
using Divergic.Logging.Xunit;
using FluentAssertions;
using Proto.Cluster.IdentityLookup;
using Proto.Remote.Tests.Messages;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Cluster.Tests
{
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
        public PidCacheTests(ITestOutputHelper testOutputHelper)
        {
            var factory = LogFactory.Create(testOutputHelper);
            Log.SetLoggerFactory(factory);
        }
        
        [Fact]
        public async Task PurgesPidCacheOnNullResponse()
        {
            var system = new ActorSystem();
            var props = Props.FromProducer(() => new EchoActor());
            var pid = system.Root.SpawnNamed(props,"stopped");
            var pid2 = system.Root.SpawnNamed(props,"alive");
            await system.Root.StopAsync(pid);
            
            var dummyIdentityLookup = new DummyIdentityLookup(pid2);
            var pidCache = new PidCache();
            
            var logger = Log.CreateLogger("dummylog");
            pidCache.TryAdd("kind", "identity", pid);
            var requestAsyncStrategy = new DefaultClusterContext(dummyIdentityLookup,pidCache,system.Root,logger);

            var res = await requestAsyncStrategy.RequestAsync<Pong>("identity", "kind", new Ping{ Message = "msg"}, new CancellationTokenSource(60000).Token
            );

            res.Message.Should().Be(":msg");
            var foundInCache = pidCache.TryGet("kind","identity",out var pidInCache);
            foundInCache.Should().BeTrue();
            pidInCache.Should().BeEquivalentTo(pid2);
        }
    }
}