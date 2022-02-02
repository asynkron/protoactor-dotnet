// -----------------------------------------------------------------------
// <copyright file="IGossipInternal.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Proto.Logging;

namespace Proto.Cluster.Gossip
{
    /// <summary>
    /// memberStateDelta is the delta state
    /// member is the target member
    /// logger is the instance logger
    /// </summary>
    public delegate void SendStateAction(MemberStateDelta memberStateDelta, Member member, InstanceLogger? logger);

    internal interface IGossip : IGossipStateStore, IGossipConsensusChecker, IGossipCore
    {
    }

    internal interface IGossipCore
    {
        Task UpdateClusterTopology(ClusterTopology clusterTopology);

        /// <summary>
        /// Called when a member receives a gossip
        /// </summary>
        /// <param name="remoteState"></param>
        /// <returns></returns>
        ImmutableList<GossipUpdate> ReceiveState(GossipState remoteState);

        /// <summary>
        /// Sends the gossip to a random set of receiving members
        /// </summary>
        /// <param name="sendStateToMember"></param>
        void SendState(SendStateAction sendStateToMember);

        MemberStateDelta GetMemberStateDelta(string targetMemberId);
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
}