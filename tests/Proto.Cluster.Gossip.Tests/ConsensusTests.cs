using System.Threading.Tasks;
using FluentAssertions;
using Google.Protobuf.WellKnownTypes;
using Proto.Cluster.Tests;
using Proto.Cluster.Gossip;
using Proto;
using Xunit;

namespace Proto.Cluster.Gossip.Tests;

public class ConsensusTests
{
    [Fact]
    public async Task ReachesConsensusForSameValue()
    {
        await using var clusterFixture = new InMemoryClusterFixture();
        await clusterFixture.InitializeAsync();

        const string key = "value";
        const int expected = 5;

        foreach (var member in clusterFixture.Members)
        {
            await member.Gossip.SetStateAsync(key, new Int32Value { Value = expected });
        }

        using var handle = clusterFixture.Members[0].Gossip.RegisterConsensusCheck<Int32Value, int>(key, v => v.Value);
        var (consensus, value) = await handle.TryGetConsensus(CancellationTokens.FromSeconds(5));

        consensus.Should().BeTrue();
        value.Should().Be(expected);
    }
}
