using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Proto.Cluster.IdentityLookup;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Cluster.Tests
{
    public class LocalAffinityStrategyTests : ClusterTests
    {
        public LocalAffinityStrategyTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }


        [Fact]
        public async Task PrefersLocalPlacement()
        {
            var clusters = await SpawnMembers(2);
            await Task.Delay(1000);
            var timeout = new CancellationTokenSource(10000);

            var firstNode = clusters[0];

            foreach (var i in Enumerable.Range(0, 1000))
            {
                await firstNode.Ping(i.ToString(), "hello", timeout.Token);
            }

            var secondNode = clusters[1];
            firstNode.System.ProcessRegistry.ProcessCount.Should().BeGreaterThan(1000);
            secondNode.System.ProcessRegistry.ProcessCount.Should().BeLessThan(100);
        }

        protected override ClusterConfig GetClusterConfig(IClusterProvider clusterProvider, string clusterName,
            IIdentityLookup identityLookup) =>
            base.GetClusterConfig(clusterProvider, clusterName, identityLookup)
                .WithClusterKind(EchoActor.Kind, EchoActor.Props)
                .WithRemoteConfig(config => config.WithProtoMessages(Remote.Tests.Messages.ProtosReflection.Descriptor))
                .WithMemberStrategyBuilder((cluster, kind) => new LocalAffinityStrategy(cluster, 1100));
    }
}