using System;
using System.Threading;
using System.Threading.Tasks;
using Divergic.Logging.Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Proto.Cluster.IdentityLookup;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Cluster.Tests
{
    public class DummyIdentityLookup : IIdentityLookup
    {
        public Task<PID?> GetAsync(string identity, string kind, CancellationToken ct)
        {
            var pid = new PID("C","D");
            return Task.FromResult(pid);
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

    public class DummySenderContext : ISenderContext
    {
        public PID? Parent { get; }
        public PID? Self { get; }
        public PID? Sender { get; }
        public IActor? Actor { get; }
        public ActorSystem System { get; }
        public MessageHeader Headers { get; }
        public object? Message { get; }
        public void Send(PID target, object message) => throw new NotImplementedException();

        public void Request(PID target, object message) => throw new NotImplementedException();

        public void Request(PID target, object message, PID? sender) => throw new NotImplementedException();

        public Task<T> RequestAsync<T>(PID target, object message, TimeSpan timeout) => throw new NotImplementedException();
        
        public Task<T> RequestAsync<T>(PID target, object message) => throw new NotImplementedException();
        
        public Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken)
        {
            return target.Id == "B" ? default : Task.FromResult((T)message);
        }
    }
    public class PidCacheTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public PidCacheTests(ITestOutputHelper testOutputHelper)
        {
            var factory = LogFactory.Create(testOutputHelper);
            _testOutputHelper = testOutputHelper;
            Log.SetLoggerFactory(factory);
        }
        
        [Fact]
        public async Task PurgesPidCacheOnNullResponse()
        {
            var dummyIdentityLookup = new DummyIdentityLookup();
            var pidCache = new PidCache();
            var pid = new PID("A","B");
            var logger = Log.CreateLogger("dummylog");
            pidCache.TryAdd("kind", "identity", pid);
            
            var context = new DummySenderContext();
            var requestAsyncStrategy = new RequestAsyncStrategy(dummyIdentityLookup,pidCache,context,logger);

            var res = await requestAsyncStrategy.RequestAsync<string>("identity", "kind", "msg", new CancellationTokenSource(1000).Token
            );
            
            var foundInCache = pidCache.TryGet("kind","identity",out var pidInCache);
            foundInCache.Should().BeTrue();
            pidInCache.Should().BeEquivalentTo(new PID("C","D"));

        }
    }
}