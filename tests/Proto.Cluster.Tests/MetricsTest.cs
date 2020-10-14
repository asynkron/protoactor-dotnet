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
        [Fact]
        public async Task Can_Collect_Heartbeat_Metrics()
        {
            var agent = new InMemAgent();

            var echoProps = Props.FromProducer(() => new EchoActor());
            var cluster = new Cluster(new ActorSystem(),
                new ClusterConfig(
                        "testCluster",
                        "127.0.0.1",
                        0,
                        new TestProvider(new TestProviderOptions(), agent))
                    .WithClusterKind("hello", echoProps)
                    .WithIdentityLookup(new PartitionIdentityLookup())
            );

            await cluster.StartMemberAsync();
            

            await Task.Delay(100);
            
            var originalCount = await HeartbeatActors();
            originalCount.Should().BePositive();
            await cluster.RequestAsync<string>("1", "hello", "lo", CancellationToken.None);
            await cluster.RequestAsync<string>("2", "hello", "lo", CancellationToken.None);

            var updatedCount = await HeartbeatActors();

            updatedCount.Should().Be(originalCount + 2);

            async Task<uint> HeartbeatActors()
            {
                var heartbeatResponse = await cluster.System.Root.RequestAsync<HeartbeatResponse>(
                    new PID(cluster.System.Address, "ClusterHeartBeat"), new HeartbeatRequest()
                );
                return heartbeatResponse.ActorCount;
            }
        }
    }
}