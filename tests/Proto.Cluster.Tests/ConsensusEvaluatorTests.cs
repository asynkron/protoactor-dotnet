using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using FluentAssertions;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Proto.Cluster.Gossip;
using Xunit;

namespace Proto.Cluster.Tests;

public class ConsensusEvaluatorTests
{
    private static GossipState CreateState(params (string memberId, string key, int value)[] entries)
    {
        var state = new GossipState();

        foreach (var (memberId, key, value) in entries)
        {
            if (!state.Members.TryGetValue(memberId, out var memberState))
            {
                memberState = new GossipState.Types.GossipMemberState();
                state.Members[memberId] = memberState;
            }

            memberState.Values[key] = new GossipKeyValue
            {
                Value = Any.Pack(new Int32Value { Value = value })
            };
        }

        return state;
    }

    private static Func<KeyValuePair<string, GossipState.Types.GossipMemberState>, (string member, string key, int value)> Map(string key)
        => kv =>
        {
            var val = kv.Value.Values.TryGetValue(key, out var gkv)
                ? gkv.Value.Unpack<Int32Value>().Value
                : default;
            return (kv.Key, key, val);
        };

    [Fact]
    public void SingleKey_AgreeingValues()
    {
        var state = CreateState(("a", "k", 1), ("b", "k", 1));
        var members = ImmutableHashSet.Create("a", "b");

        var (consensus, value, _) = ConsensusEvaluator.HasConsensus(state, members, new[] { Map("k") });
        consensus.Should().BeTrue();
        value.Should().Be(1);
    }

    [Fact]
    public void SingleKey_MissingMember()
    {
        var state = CreateState(("a", "k", 1));
        var members = ImmutableHashSet.Create("a", "b");

        var (consensus, _, _) = ConsensusEvaluator.HasConsensus(state, members, new[] { Map("k") });
        consensus.Should().BeFalse();
    }

    [Fact]
    public void SingleKey_ConflictingValues()
    {
        var state = CreateState(("a", "k", 1), ("b", "k", 2));
        var members = ImmutableHashSet.Create("a", "b");

        var (consensus, _, _) = ConsensusEvaluator.HasConsensus(state, members, new[] { Map("k") });
        consensus.Should().BeFalse();
    }

    [Fact]
    public void MultiKey_AgreeingValues()
    {
        var state = CreateState(("a", "k1", 1), ("a", "k2", 1), ("b", "k1", 1), ("b", "k2", 1));
        var members = ImmutableHashSet.Create("a", "b");

        var (consensus, value, _) = ConsensusEvaluator.HasConsensus(state, members, new[] { Map("k1"), Map("k2") });
        consensus.Should().BeTrue();
        value.Should().Be(1);
    }

    [Fact]
    public void MultiKey_MissingMember()
    {
        var state = CreateState(("a", "k1", 1), ("a", "k2", 1));
        var members = ImmutableHashSet.Create("a", "b");

        var (consensus, _, _) = ConsensusEvaluator.HasConsensus(state, members, new[] { Map("k1"), Map("k2") });
        consensus.Should().BeFalse();
    }

    [Fact]
    public void MultiKey_ConflictingValues()
    {
        var state = CreateState(("a", "k1", 1), ("a", "k2", 1), ("b", "k1", 1), ("b", "k2", 2));
        var members = ImmutableHashSet.Create("a", "b");

        var (consensus, _, _) = ConsensusEvaluator.HasConsensus(state, members, new[] { Map("k1"), Map("k2") });
        consensus.Should().BeFalse();
    }
}
