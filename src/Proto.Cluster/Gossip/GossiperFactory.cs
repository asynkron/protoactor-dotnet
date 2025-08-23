// -----------------------------------------------------------------------
// <copyright file="GossiperFactory.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Proto.Remote;

namespace Proto.Cluster.Gossip;

public partial class Gossiper
{
    /// <summary>
    ///     Creates a <see cref="Gossiper"/> using the traditional cluster dependencies.
    /// </summary>
    public static Gossiper FromCluster(Cluster cluster)
    {
        var options = new GossiperOptions(
            cluster.System.Root,
            cluster.MemberList,
            cluster.System.Remote().BlockList,
            cluster.System.EventStream,
            cluster.System.Id,
            cluster.JoinedCluster,
            cluster.System.Shutdown,
            () =>
            {
                var stats = new ActorStatistics();
                foreach (var k in cluster.GetClusterKinds())
                {
                    var kind = cluster.GetClusterKind(k);
                    stats.ActorCount.Add(k, kind.Count);
                }

                return stats;
            },
            cluster.Config.GossipFanout,
            cluster.Config.GossipMaxSend,
            cluster.Config.GossipInterval,
            cluster.Config.GossipRequestTimeout,
            cluster.Config.GossipDebugLogging,
            cluster.Config.HeartbeatExpiration,
            () => cluster.Config.HeartbeatExpirationHandler(cluster)
        );

        return new Gossiper(options);
    }
}
