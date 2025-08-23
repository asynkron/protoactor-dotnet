using System.Collections.Immutable;
using System.Linq;
using FluentAssertions;
using Proto.Cluster.Gossip;
using Xunit;

namespace Proto.Cluster.Tests;

public class MemberStateDeltaBuilderTests
{
    [Fact]
    public void Build_FiltersUsingWatermarks()
    {
        var state = new GossipState();

        var memberA = new GossipState.Types.GossipMemberState();
        memberA.Values.Add("k1", new GossipKeyValue { SequenceNumber = 1 });
        memberA.Values.Add("k2", new GossipKeyValue { SequenceNumber = 2 });
        state.Members.Add("a", memberA);

        var committed = ImmutableDictionary<string, long>.Empty
            .SetItem("c.a", 1);

        var members = state.Members.Where(m => m.Key != "c");

        var result = MemberStateDeltaBuilder.BuildOrdered(state, "c", committed, members, 10);

        result.State.Members.Should().ContainKey("a");
        result.State.Members["a"].Values.Keys.Should().BeEquivalentTo("k2");
        result.PendingOffsets["c.a"].Should().Be(2);
        result.HasState.Should().BeTrue();
    }

    [Fact]
    public void Build_RespectsMaxSend()
    {
        var state = new GossipState();

        for (var i = 0; i < 5; i++)
        {
            var ms = new GossipState.Types.GossipMemberState();
            ms.Values.Add($"k{i}", new GossipKeyValue { SequenceNumber = 1 });
            state.Members.Add($"member{i}", ms);
        }

        var members = state.Members.OrderBy(m => m.Key);

        var result = MemberStateDeltaBuilder.BuildOrdered(state, "target", ImmutableDictionary<string, long>.Empty, members, 3);

        result.State.Members.Count.Should().BeLessOrEqualTo(3);
    }
}
