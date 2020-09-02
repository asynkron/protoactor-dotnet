using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Divergic.Logging.Xunit;
using Microsoft.Extensions.Logging;
using Proto.Cluster.Testing;
using Proto.Remote;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Cluster.Tests
{
    [Trait("Category", "Remote")]
    public class ClusterTests
    {
        private ILogger _logger;

        public ClusterTests(ITestOutputHelper testOutputHelper)
        {
            var factory = LogFactory.Create(testOutputHelper);
            Log.SetLoggerFactory(factory);
            _logger = Log.CreateLogger<ClusterTests>();
        }

        [Fact]
        public void InMemAgentRegisterService()
        {
            var agent = new InMemAgent();
            agent.RegisterService(new AgentServiceRegistration
            {
                ID = Guid.NewGuid(),
                Address = "LocalHost",
                Kinds = new[] { "SomeKind" },
                Port = 8080
            });

            var services = agent.GetServicesHealth();

            Assert.True(services.Length == 1, "There should be only one service");

        }

        [Fact]
        public void InMemAgentServiceShouldBeAlive()
        {
            var agent = new InMemAgent();
            agent.RegisterService(new AgentServiceRegistration
            {
                ID = Guid.NewGuid(),
                Address = "LocalHost",
                Kinds = new[] { "SomeKind" },
                Port = 8080
            });

            var services = agent.GetServicesHealth();
            var first = services.First();
            Assert.True(first.Alive);
        }

        [Fact]
        public async Task InMemAgentServiceShouldNotBeAlive()
        {
            var agent = new InMemAgent();
            agent.RegisterService(new AgentServiceRegistration
            {
                ID = Guid.NewGuid(),
                Address = "LocalHost",
                Kinds = new[] { "SomeKind" },
                Port = 8080
            });

            var services = agent.GetServicesHealth();
            var first = services.First();

            await Task.Delay(TimeSpan.FromSeconds(5));

            Assert.False(first.Alive);
        }

        [Fact]
        public async Task InMemAgentServiceShouldBeAliveAfterTTLRefresh()
        {
            var id = Guid.NewGuid();
            var agent = new InMemAgent();
            agent.RegisterService(new AgentServiceRegistration
            {
                ID = id,
                Address = "LocalHost",
                Kinds = new[] { "SomeKind" },
                Port = 8080
            });


            await Task.Delay(TimeSpan.FromSeconds(5));
            agent.RefreshServiceTTL(id);

            var services = agent.GetServicesHealth();
            var first = services.First();

            Assert.True(first.Alive);
        }


        [Fact]
        public async Task ClusterShouldContainOneAliveNode()
        {
            var agent = new InMemAgent();

            var cluster = await NewCluster(agent, 8080);

            var services = agent.GetServicesHealth();
            var first = services.First();
            Assert.True(first.Alive);
            Assert.True(services.Length == 1);

            await cluster.ShutdownAsync();
        }

        [Fact]
        public async Task ClusterShouldRefreshServiceTTL()
        {
            var agent = new InMemAgent();

            var cluster = await NewCluster(agent, 8080);

            var services = agent.GetServicesHealth();
            var first = services.First();
            var ttl1 = first.TTL;
            SpinWait.SpinUntil(() => ttl1 != first.TTL, TimeSpan.FromSeconds(10));
            Assert.NotEqual(ttl1, first.TTL);
            await cluster.ShutdownAsync();
        }

        [Fact]
        public async Task ClusterShouldContainTwoAliveNodes()
        {
            var agent = new InMemAgent();

            var cluster1 = await NewCluster(agent, 8080);
            var cluster2 = await NewCluster(agent, 8081);

            var services = agent.GetServicesHealth();

            Assert.True(services.Length == 2);
            Assert.True(services.All(m => m.Alive));

            await cluster1.ShutdownAsync();
            await cluster2.ShutdownAsync();
        }

        [Fact]
        public async Task ClusterShouldContainOneAliveAfterShutdownOfNode1()
        {
            var agent = new InMemAgent();

            var cluster1 = await NewCluster(agent, 8080);
            var cluster2 = await NewCluster(agent, 8081);

            await cluster1.ShutdownAsync();

            var services = agent.GetServicesHealth();

            Assert.True(services.Length == 1, "Expected 1 Node");
            Assert.True(services.All(m => m.Alive));


            await cluster2.ShutdownAsync();
        }

        [Fact]
        public async Task ClusterShouldSpawnActors()
        {
            var agent = new InMemAgent();

            var prop = Props.FromFunc(context =>
                {
                    if (context.Message is string _)
                    {
                        context.Respond("Hello");
                    }

                    return Actor.Done;
                }
            );

            var cluster1 = await NewCluster(agent, 8080, ("echo", prop));
            var cluster2 = await NewCluster(agent, 8081, ("echo", prop));

            // Interactions are disabled for 3 seconds by default
            await Task.Delay(5_000);
            var pid = await cluster1.GetAsync("myactor", "echo");

            _logger.LogDebug("PID = {0}", pid);

            Assert.NotNull(pid);

            await cluster1.ShutdownAsync(false);
            await cluster2.ShutdownAsync(false);
        }

        private static async Task<Cluster> NewCluster(InMemAgent agent, int port,
            params (string kind, Props prop)[] kinds)
        {
            var provider = new TestProvider(new TestProviderOptions(), agent);
            var config = new ClusterConfig("cluster1", provider)
               .WithPidCache(false);
            var system = new ActorSystem();
            var remote = new SelfHostedRemote(system, remote =>
            {
                foreach (var (kind, prop) in kinds)
                {
                    remote.RemoteKindRegistry.RegisterKnownKind(kind, prop);
                }
            });
            var cluster = new Cluster(system, config);
            await cluster.StartAsync();
            return cluster;
        }
    }
}
