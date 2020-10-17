using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Proto.Remote.Tests.Messages;
using Xunit;

namespace Proto.Cluster.Tests
{
    public class LocalAffinityStrategyTests: ClusterFixture
    {

        [Fact]
        public async Task PrefersLocalPlacement()
        {

            var clusters = await SpawnClusters(2, 
                config => config.WithMemberStrategyBuilder((cluster, kind) => new LocalAffinityStrategy(cluster, 1100)));
            await Task.Delay(1000);

            var firstNode = clusters[0];

            foreach (var i in Enumerable.Range(0, 1000))
            {
                await firstNode.Ping(i.ToString(), "hello");
            }

            var secondNode = clusters[1];
            firstNode.System.ProcessRegistry.ProcessCount.Should().BeGreaterThan(1000);
            secondNode.System.ProcessRegistry.ProcessCount.Should().BeLessThan(50);

        }
        
    }
}