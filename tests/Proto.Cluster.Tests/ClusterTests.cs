using System;
using System.Linq;
using System.Threading.Tasks;
using Proto.Cluster.Testing;
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
    }
}