// -----------------------------------------------------------------------
// <copyright file="IGossipInternal.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace Proto.Cluster.Gossip;

internal interface IGossip : IGossipStateStore, IGossipConsensusChecker, IGossipCore
{
}

internal interface IGossipCore
{
    Task UpdateClusterTopology(ClusterTopology clusterTopology);

    /// <summary>
    ///     Called when a member receives a gossip
    /// </summary>
    /// <param name="remoteState"></param>
    /// <returns></returns>
    ImmutableList<GossipUpdate> ReceiveState(GossipState remoteState);

    /// <summary>
    ///     Produces the gossip state for a random set of receiving members
    /// </summary>
    IEnumerable<(Member member, MemberStateDelta memberState)> SendState();
}

internal interface IGossipConsensusChecker
{
    void AddConsensusCheck(string id, ConsensusCheck check);

    void RemoveConsensusCheck(string id);
}

internal interface IGossipStateStore
{
    GossipState GetStateSnapshot();

    ImmutableDictionary<string, Any> GetState(string key);

    ImmutableDictionary<string, GossipKeyValue> GetStateEntry(string key);

    void SetState(string key, IMessage value);
}