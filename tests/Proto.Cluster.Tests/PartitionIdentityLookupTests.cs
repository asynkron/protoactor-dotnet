using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Proto.Cluster.IdentityLookup;
using Proto.Remote.Tests.Messages;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Cluster.Tests
{
    public class PartitionIdentityLookupTests : ClusterTestTemplate
    {
        public PartitionIdentityLookupTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

        [Theory]
        [InlineData(1, 1000, 5000)]
        [InlineData(3, 1000, 5000)]
        public async Task CanSpawnConcurrently(int clusterNodes, int count, int msTimeout)
        {
            var timeout = new CancellationTokenSource(msTimeout).Token;

            var clusterMembers = await SpawnMembers(clusterNodes);

            await PingAllConcurrently(clusterMembers[0]);

            async Task PingAllConcurrently(Cluster cluster)
            {
                await Task.WhenAll(
                    Enumerable.Range(1, count).Select(async i =>
                        {
                            var id = i.ToString();
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
        [InlineData(1, 1000, 5000)]
        [InlineData(3, 1000, 8000)]
        public async Task CanSpawnSequentially(int clusterNodes, int count, int msTimeout)
        {
            var timeout = new CancellationTokenSource(msTimeout).Token;

            var clusterMembers = await SpawnMembers(clusterNodes);


            await PingAllSequentially(clusterMembers[0]);

            async Task PingAllSequentially(Cluster cluster)
            {
                foreach (var i in Enumerable.Range(1, count))
                {
                    var id = i.ToString();
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

        protected override ClusterConfig GetClusterConfig(IClusterProvider clusterProvider, string clusterName,
            IIdentityLookup identityLookup)
        {
            var config = base.GetClusterConfig(clusterProvider, clusterName, identityLookup)
                .WithClusterKind(EchoActor.Kind, EchoActor.Props);
            config.RemoteConfig.WithProtoMessages(Remote.Tests.Messages.ProtosReflection.Descriptor);
            return config;
        }
    }
}