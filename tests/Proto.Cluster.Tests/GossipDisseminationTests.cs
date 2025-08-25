using System;
using System.Threading.Tasks;
using FluentAssertions;
using Google.Protobuf.WellKnownTypes;
using Proto.Cluster.Gossip;
using static Proto.TestKit.TestKit;
using Xunit;

namespace Proto.Cluster.Tests;

[Collection("ClusterTests")]
public class GossipDisseminationTests
{
    [Fact]
    public async Task DisseminatesStateAcrossMembers()
    {
        await using var clusterFixture = new InMemoryClusterFixture();
        await clusterFixture.InitializeAsync();

        var source = clusterFixture.Members[0];
        var target = clusterFixture.Members[1];

        await source.Gossip.SetStateAsync("shared", new Int32Value { Value = 99 });

        Int32Value? value = null;
        await AwaitConditionAsync(async () =>
        {
            var state = await target.Gossip.GetState<Int32Value>("shared");
            return state.TryGetValue(source.System.Id, out value);
        }, TimeSpan.FromSeconds(20));

        value!.Value.Should().Be(99);
    }
}
