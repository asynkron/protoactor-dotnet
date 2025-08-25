using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Proto;

namespace Proto.Cluster.Gossip;

public delegate (bool, T) ConsensusCheck<T>(GossipState state, IImmutableSet<string> memberIds) where T : notnull;

public partial class Gossiper
{
    private async Task BlockGracefullyLeft()
    {
        var gracefullyLeftEntries = await GetStateEntry(GossipKeys.GracefullyLeft).ConfigureAwait(false);

        var alreadyBlocked = _blockList.BlockedMembers;

        var gracefullyLeft = new List<string>();

        foreach (var memberId in gracefullyLeftEntries.Keys)
        {
            if (!alreadyBlocked.Contains(memberId) && memberId != _systemId)
            {
                gracefullyLeft.Add(memberId);
            }
        }

        if (gracefullyLeft.Count > 0)
        {
            _blockList.Block(gracefullyLeft, "Gracefully left");
        }
    }

    private async Task BlockExpiredHeartbeats()
    {
        if (_options.HeartbeatExpiration == TimeSpan.Zero)
        {
            return;
        }

        await _options.HeartbeatExpirationHandler().ConfigureAwait(false);
    }

    private ActorStatistics GetActorStatistics() => _getActorStatistics();

    public IConsensusHandle<TV> RegisterConsensusCheck<T, TV>(string key, Func<T, TV?> getValue)
        where T : notnull, IMessage, new()
        where TV : notnull =>
        RegisterConsensusCheck(ConsensusCheckBuilder<TV>.Create(key, getValue));

    public IConsensusHandle<T> RegisterConsensusCheck<T>(IConsensusCheckDefinition<T> consensusDefinition)
        where T : notnull
    {
        var cts = new CancellationTokenSource();
        var (consensusHandle, check) = consensusDefinition.Build(cts.Cancel);
        _context.Send(_pid, new AddConsensusCheck(check, cts.Token));

        return consensusHandle;
    }
}

