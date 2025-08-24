// -----------------------------------------------------------------------
// <copyright file="GossipDefaults.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Linq;
using System.Threading.Tasks;

namespace Proto.Cluster.Gossip;

/// <summary>
///     Default handlers used by the gossip system.
/// </summary>
public static class GossipDefaults
{
    /// <summary>
    ///     Blocks members whose heartbeat has expired.
    /// </summary>
    public static async Task BlockExpiredMembers(Cluster cluster)
    {
        var gossipState = await cluster.Gossip.GetStateEntry(GossipKeys.Heartbeat).ConfigureAwait(false);
        var blockList = cluster.Remote.BlockList;
        var alreadyBlocked = blockList.BlockedMembers;
        //new blocked members
        var blocked = (from x in gossipState
                //never block ourselves
                where x.Key != cluster.System.Id
                //pick any entry that is too old
                where x.Value.Age > cluster.Config.HeartbeatExpiration
                //and not already part of the block list
                where !alreadyBlocked.Contains(x.Key)
                select x.Key)
            .ToArray();

        if (blocked.Length != 0)
        {
            blockList.Block(blocked, "Expired heartbeat");
        }
    }
}
