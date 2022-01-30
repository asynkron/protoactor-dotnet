// -----------------------------------------------------------------------
// <copyright file = "GossipKeys.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Proto.Cluster.Gossip
{
    public static class GossipKeys
    {
        public const string Topology = "cluster:topology";
        public const string Heartbeat = "cluster:heartbeat";
        public const string GracefullyLeft = "cluster:left";
    }
}