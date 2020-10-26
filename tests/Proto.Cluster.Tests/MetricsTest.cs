using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Divergic.Logging.Xunit;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Cluster.Tests
{
    public class MetricsTest: ClusterFixture
    {
        public MetricsTest(ITestOutputHelper testOutputHelper)
        {
            var factory = LogFactory.Create(testOutputHelper);
            Log.SetLoggerFactory(factory);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        public async Task CanCollectHeartbeatMetrics(int clusterSize)
        {
            var timeout = new CancellationTokenSource(5000);

            var clusters = await SpawnClusterNodes(clusterSize);

            await PingAll("ping1",timeout.Token);
            var count = await GetActorCountFromHeartbeat();
            count.Should().BePositive();

            const int virtualActorCount = 10;
            foreach (var id in Enumerable.Range(1, virtualActorCount))
            {
                await PingAll(id.ToString(), timeout.Token);
            }

            var afterPing = await GetActorCountFromHeartbeat();

            afterPing.Should().Be(count + virtualActorCount, "We expect the echo actors to be added to the count");


            async Task<int> GetActorCountFromHeartbeat()
            {
                var heartbeatResponses = await Task.WhenAll(clusters.Select(c =>
                        c.System.Root.RequestAsync<HeartbeatResponse>(
                            PID.FromAddress(c.System.Address, "ClusterHeartBeat"), new HeartbeatRequest(), timeout.Token
                        )
                    )
                );
                return heartbeatResponses.Select(response => (int) response.ActorCount).Sum();
            }

            async Task PingAll(string identity, CancellationToken token)
            {
                foreach (var cluster in clusters)
                {
                    await cluster.Ping(identity, "", token);
                }
            }
        }

        
    }
}