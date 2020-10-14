using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Proto.Cluster.Partition;
using Proto.Cluster.Testing;
using Xunit;

namespace Proto.Cluster.Tests
{
    public class MetricsTest
    {
        private static Props EchoProps = Props.FromProducer(() => new EchoActor());

        
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public async Task CanCollectHeartbeatMetrics(int clusterSize)
        {

            var clusters = await SpawnClusters(clusterSize);
            var cluster = clusters.First();
            
            var count = await HeartbeatActors();
            count.Should().BePositive();
            
            async Task<int> HeartbeatActors()
            {
                var heartbeatResponses = await Task.WhenAll(clusters.Select(c =>
                        c.System.Root.RequestAsync<HeartbeatResponse>(
                            new PID(c.System.Address, "ClusterHeartBeat"), new HeartbeatRequest()
                        )
                    )
                );
                return heartbeatResponses.Select(response => (int)response.ActorCount).Sum();
            }
        }

        private async Task<IList<Cluster>> SpawnClusters(int count)
        {
            var agent = new InMemAgent();
            var clusterTasks = Enumerable.Range(0, count).Select(_ => SpawnCluster(agent))
                .ToList();
            await Task.WhenAll(clusterTasks);
            return clusterTasks.Select(task => task.Result).ToList();
        }
        
        private async Task<Cluster> SpawnCluster(InMemAgent agent)
        {
            var cluster = new Cluster(new ActorSystem(),
                new ClusterConfig(
                        "testCluster",
                        "127.0.0.1",
                        0,
                        new TestProvider(new TestProviderOptions(), agent))
                    .WithClusterKind("hello", EchoProps)
                    .WithIdentityLookup(new PartitionIdentityLookup())
            );
            await cluster.StartMemberAsync();
            return cluster;
        }
    }
}