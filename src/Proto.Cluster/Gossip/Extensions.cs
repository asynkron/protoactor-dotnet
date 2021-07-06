// -----------------------------------------------------------------------
// <copyright file="Extensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Runtime.InteropServices.ComTypes;

namespace Proto.Cluster.Gossip
{
    public static class Extensions
    {
        public static ClusterTopology? GetTopology(this GossipState self, string memberId)
        {
            if (!self.Members.TryGetValue(memberId, out var memberState))
                return null;

            return memberState.GetTopology();
        }
        
        public static ClusterTopology? GetTopology(this GossipState.Types.GossipMemberState memberState)
        {
            if (!memberState.Values.TryGetValue("topology", out var entry))
                return null;

            var topology = entry.Value.Unpack<ClusterTopology>();
            return topology;
        }
    }
}