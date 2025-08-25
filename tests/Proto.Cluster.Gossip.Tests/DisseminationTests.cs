using System.Threading.Tasks;
using FluentAssertions;
using Google.Protobuf.WellKnownTypes;
using Proto.Cluster.Tests;
using Proto.Cluster.Gossip;
using Proto;
using Xunit;

namespace Proto.Cluster.Gossip.Tests;

public class DisseminationTests
{
    [Fact]
    public async Task DisseminatesStateAcrossMembers()
    {
        await using var clusterFixture = new InMemoryClusterFixture();
        await clusterFixture.InitializeAsync();

        var source = clusterFixture.Members[0];
        var target = clusterFixture.Members[1];

        await source.Gossip.SetStateAsync("shared", new Int32Value { Value = 99 });

        var ct = CancellationTokens.FromSeconds(20);
        Int32Value? value = null;
        while (!ct.IsCancellationRequested)
        {
            var state = await target.Gossip.GetState<Int32Value>("shared");
            if (state.TryGetValue(source.System.Id, out value))
            {
                break;
            }
            await Task.Delay(100, ct);
        }

        value.Should().NotBeNull();
        value!.Value.Should().Be(99);
    }
}
