using System.Threading.Tasks;
using FluentAssertions;
using Google.Protobuf.WellKnownTypes;
using Proto.Cluster.Tests;
using Proto.Cluster.Gossip;
using Xunit;

namespace Proto.Cluster.Gossip.Tests;

public class StateQueryTests
{
    [Fact]
    public async Task CanSetAndGetState()
    {
        await using var clusterFixture = new InMemoryClusterFixture();
        await clusterFixture.InitializeAsync();

        var member = clusterFixture.Members[0];

        await member.Gossip.SetStateAsync("answer", new Int32Value { Value = 42 });
        var state = await member.Gossip.GetState<Int32Value>("answer");

        state[member.System.Id].Value.Should().Be(42);
    }
}
