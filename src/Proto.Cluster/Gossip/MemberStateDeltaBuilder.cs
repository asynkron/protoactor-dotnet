using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;

namespace Proto.Cluster.Gossip;

internal class MemberStateDeltaBuilder
{
    private readonly string _myId;
    private readonly int _gossipMaxSend;

    public MemberStateDeltaBuilder(string myId, int gossipMaxSend)
    {
        _myId = myId;
        _gossipMaxSend = gossipMaxSend;
    }

    public MemberStateDeltaBuildResult Build(
        GossipState currentState,
        string targetMemberId,
        ImmutableDictionary<string, long> committedOffsets,
        Random rnd)
    {
        var members = currentState
            .Members
            .Where(m => m.Key != targetMemberId)
            .OrderByRandom(rnd, m => m.Key == _myId);

        return BuildOrdered(currentState, targetMemberId, committedOffsets, members, _gossipMaxSend);
    }

    /// <summary>
    /// Calculates a member state delta based on a deterministic member sequence.
    /// Returns a new state, updated offsets and a flag indicating if any state was sent.
    /// </summary>
    public static MemberStateDeltaBuildResult BuildOrdered(
        GossipState currentState,
        string targetMemberId,
        ImmutableDictionary<string, long> committedOffsets,
        IEnumerable<KeyValuePair<string, GossipState.Types.GossipMemberState>> members,
        int gossipMaxSend)
    {
        var newState = new GossipState();
        var pendingOffsets = committedOffsets;
        var count = 0;

        foreach (var (memberId, memberState) in members)
        {
            var newMemberState = new GossipState.Types.GossipMemberState();
            var watermarkKey = $"{targetMemberId}.{memberId}";
            committedOffsets.TryGetValue(watermarkKey, out var watermark);
            var newWatermark = watermark;

            foreach (var (key, value) in memberState.Values)
            {
                if (value.SequenceNumber <= watermark)
                {
                    continue;
                }

                if (value.SequenceNumber > newWatermark)
                {
                    newWatermark = value.SequenceNumber;
                }

                newMemberState.Values.Add(key, value);
            }

            if (newMemberState.Values.Count > 0)
            {
                newState.Members.Add(memberId, newMemberState);
                pendingOffsets = pendingOffsets.SetItem(watermarkKey, newWatermark);
                count++;
                if (count >= gossipMaxSend)
                {
                    break;
                }
            }
        }

        var hasState = committedOffsets != pendingOffsets;
        return new MemberStateDeltaBuildResult(newState, pendingOffsets, hasState);
    }
}

internal record MemberStateDeltaBuildResult(
    GossipState State,
    ImmutableDictionary<string, long> PendingOffsets,
    bool HasState);
