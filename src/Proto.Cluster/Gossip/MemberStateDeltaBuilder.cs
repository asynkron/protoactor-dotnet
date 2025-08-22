using System;
using System.Collections.Immutable;
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
        var newState = new GossipState();
        var pendingOffsets = committedOffsets;
        var count = 0;

        var members = currentState
            .Members
            .Where(m => m.Key != targetMemberId)
            .OrderByRandom(rnd, m => m.Key == _myId);

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
                if (count >= _gossipMaxSend)
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
