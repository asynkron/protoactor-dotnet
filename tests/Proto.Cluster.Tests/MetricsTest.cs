namespace Proto.Cluster.Tests
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Xunit;

    [Collection("ClusterTests")]
    public class MetricsTest : ClusterTest, IClassFixture<InMemoryClusterFixture>
    {
        public MetricsTest(InMemoryClusterFixture clusterFixture) : base(clusterFixture)
        {
        }


        [Fact]
        public async Task CanCollectHeartbeatMetrics()
        {
            var timeout = new CancellationTokenSource(5000);


            await PingAll("ping1", timeout.Token);
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
                var heartbeatResponses = await Task.WhenAll(Members.Select(c =>
                        c.System.Root.RequestAsync<HeartbeatResponse>(
                            PID.FromAddress(c.System.Address, "ClusterHeartBeat"), new HeartbeatRequest(), timeout.Token
                        )
                    )
                );
                return heartbeatResponses.Select(response => (int) response.ActorCount).Sum();
            }

            async Task PingAll(string identity, CancellationToken token)
            {
                foreach (var cluster in Members)
                {
                    await cluster.Ping(identity, "", token);
                }
            }
        }
    }
}