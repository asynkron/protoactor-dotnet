namespace Proto.Cluster.Tests
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Remote.Tests.Messages;
    using Xunit;
    using Xunit.Abstractions;

    public class PartitionIdentityLookupTests : ClusterTest, IClassFixture<InMemoryClusterFixture>
    {
        private ITestOutputHelper TestOutputHelper { get; }

        public PartitionIdentityLookupTests(InMemoryClusterFixture clusterFixture, ITestOutputHelper testOutputHelper) :
            base(clusterFixture)
        {
            TestOutputHelper = testOutputHelper;
        }

        [Theory]
        [InlineData(1000, 5000)]
        public async Task CanSpawnConcurrently(int count, int msTimeout)
        {
            var timeout = new CancellationTokenSource(msTimeout).Token;


            await PingAllConcurrently(Members[0]);

            async Task PingAllConcurrently(Cluster cluster)
            {
                await Task.WhenAll(
                    GetActorIds(count).Select(async id =>
                        {
                            Pong pong = null;
                            while (pong == null)
                            {
                                timeout.ThrowIfCancellationRequested();
                                pong = await cluster.Ping(id, id, timeout);
                                TestOutputHelper.WriteLine($"{id} received response {pong?.Message}");
                            }

                            pong.Message.Should().Be($"{id}:{id}");
                        }
                    )
                );
            }
        }

        [Theory]
        [InlineData(1000, 5000)]
        public async Task CanSpawnSequentially(int count, int msTimeout)
        {
            var timeout = new CancellationTokenSource(msTimeout).Token;

            await PingAllSequentially(Members[0]);

            async Task PingAllSequentially(Cluster cluster)
            {
                foreach (var id in GetActorIds(count))
                {
                    Pong pong = null;
                    while (pong == null)
                    {
                        timeout.ThrowIfCancellationRequested();
                        pong = await cluster.Ping(id, id, timeout);
                    }

                    pong.Message.Should().Be($"{id}:{id}");
                }
            }
        }
    }
}