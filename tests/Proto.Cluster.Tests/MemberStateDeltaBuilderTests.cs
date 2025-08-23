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

    [Fact]
    public void Build_UsesRandomProviderOrdering()
    {
        var state = new GossipState();

        var me = new GossipState.Types.GossipMemberState();
        me.Values.Add("k-me", new GossipKeyValue { SequenceNumber = 1 });
        state.Members.Add("me", me);

        var a = new GossipState.Types.GossipMemberState();
        a.Values.Add("k-a", new GossipKeyValue { SequenceNumber = 1 });
        state.Members.Add("a", a);

        var b = new GossipState.Types.GossipMemberState();
        b.Values.Add("k-b", new GossipKeyValue { SequenceNumber = 1 });
        state.Members.Add("b", b);

        var committed = ImmutableDictionary<string, long>.Empty;
        var rnd = new DeterministicRandomProvider(new[] { 5, 1, 2 });
        var builder = new MemberStateDeltaBuilder("me", 10);

        var result = builder.Build(state, "target", committed, rnd);

        result.State.Members.Keys.Should().Equal("me", "a", "b");
        result.State.Members["me"].Values.Keys.Should().Equal("k-me");
        result.State.Members["a"].Values.Keys.Should().Equal("k-a");
        result.State.Members["b"].Values.Keys.Should().Equal("k-b");
    }
}
