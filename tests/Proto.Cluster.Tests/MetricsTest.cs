using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Divergic.Logging.Xunit;
using FluentAssertions;
using Proto.Cluster.Partition;
using Proto.Cluster.Testing;
using Proto.Remote.Tests.Messages;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Cluster.Tests
{
    public class MetricsTest
    {
        public MetricsTest(ITestOutputHelper testOutputHelper)
        {
            var factory = LogFactory.Create(testOutputHelper);
            Log.SetLoggerFactory(factory);
        }
        
        private static readonly Props EchoProps = Props.FromProducer(() => new EchoActor());

        
        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        public async Task CanCollectHeartbeatMetrics(int clusterSize)
        {
            var timeout = new CancellationTokenSource(5000);

            var clusters = await SpawnClusters(clusterSize);

            await PingAll("ping1");
            var count = await GetActorCountFromHeartbeat();
            count.Should().BePositive();

            const int virtualActorCount = 10;
            foreach (var id in Enumerable.Range(1, virtualActorCount))
            {
                await PingAll(id.ToString());
            }

            var afterPing = await GetActorCountFromHeartbeat();

            afterPing.Should().Be(count + virtualActorCount, "We expect the echo actors to be added to the count");
            
            
            
            async Task<int> GetActorCountFromHeartbeat()
            {
                var heartbeatResponses = await Task.WhenAll(clusters.Select(c =>
                        c.System.Root.RequestAsync<HeartbeatResponse>(
                            new PID(c.System.Address, "ClusterHeartBeat"), new HeartbeatRequest(), timeout.Token
                        )
                    )
                );
                return heartbeatResponses.Select(response => (int)response.ActorCount).Sum();
            }

            async Task PingAll(string identity)
            {
                foreach (var cluster in clusters)
                {
                    await cluster.RequestAsync<Pong>(identity, EchoActor.Kind, new Ping(), CancellationToken.None);
                }
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
                    .WithRemoteConfig(config => config
                        .WithProtoMessages(Proto.Remote.Tests.Messages.ProtosReflection.Descriptor)
                    )
                    .WithClusterKind(EchoActor.Kind, EchoProps)
                    .WithIdentityLookup(new PartitionIdentityLookup())
            );
            await cluster.StartMemberAsync();
            return cluster;
        }
    }
}