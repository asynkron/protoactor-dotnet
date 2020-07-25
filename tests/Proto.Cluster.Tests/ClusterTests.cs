using System;
using System.Linq;
using System.Threading.Tasks;
using Proto.Cluster.Testing;
using Proto.Remote;
using Xunit;

namespace Proto.Cluster.Tests
{
    [Trait("Category", "Remote")]
    public class ClusterTests
    {
        
        [Fact]
        public void InMemAgentRegisterService()
        {
            var agent = new InMemAgent();
            agent.RegisterService(new AgentServiceRegistration
            {
                ID = "abc",
                Address = "LocalHost",
                Kinds = new []{"SomeKind"},
                Port = 8080
            });

            var services = agent.GetServicesHealth();
            
            Assert.True(services.Length == 1,"There should be only one service");

        }
        
        [Fact]
        public void InMemAgentServiceShouldBeAlive()
        {
            var agent = new InMemAgent();
            agent.RegisterService(new AgentServiceRegistration
            {
                ID = "abc",
                Address = "LocalHost",
                Kinds = new []{"SomeKind"},
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
                ID = "abc",
                Address = "LocalHost",
                Kinds = new []{"SomeKind"},
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
            var agent = new InMemAgent();
            agent.RegisterService(new AgentServiceRegistration
            {
                ID = "abc",
                Address = "LocalHost",
                Kinds = new []{"SomeKind"},
                Port = 8080
            });


            await Task.Delay(TimeSpan.FromSeconds(5));
            agent.RefreshServiceTTL("abc");

            var services = agent.GetServicesHealth();
            var first = services.First();
    
            Assert.True(first.Alive);
        }
        
        
        [Fact]
        public async Task ClusterShouldContainOneAliveNode()
        {
            var agent = new InMemAgent();
            
            var cluster = await NewCluster(agent,8080);

            var services = agent.GetServicesHealth();
            var first = services.First();
            Assert.True(first.Alive);
            Assert.True(services.Length == 1);
            
            await cluster.Shutdown();
        }
        
        [Fact]
        public async Task ClusterShouldContainTwoAliveNode()
        {
            var agent = new InMemAgent();
            
            var cluster1 = await NewCluster(agent,8080);
            var cluster2 = await NewCluster(agent,8081);

            var services = agent.GetServicesHealth();
  
            Assert.True(services.Length == 2);
            Assert.True(services.All(m => m.Alive));
            
            await cluster1.Shutdown();
            await cluster2.Shutdown();
        }

        private static async Task<Cluster> NewCluster(InMemAgent agent,int port)
        {
            var provider = new TestProvider(new TestProviderOptions(),  agent);
            var system = new ActorSystem();
            var serialization = new Serialization();
            var cluster = new Cluster(system, serialization);
            await cluster.Start("cluster1", "localhost", port, provider);
            return cluster;
        }
    }
}