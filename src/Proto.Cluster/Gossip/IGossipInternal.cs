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
    public interface IGossipInternal
    {
        Task UpdateClusterTopology(ClusterTopology clusterTopology);

        ImmutableDictionary<string, Any> GetState(GetGossipStateRequest getState);
        
        void SetState(string key, IMessage value);

        IReadOnlyCollection<GossipUpdate> MergeState(GossipState remoteState);

        void SendState(Action<Member, InstanceLogger?> sendGossipForMember);

        bool TryGetMemberState(string memberId, out ImmutableDictionary<string, long> pendingOffsets, out GossipState stateForMember);

        void CommitPendingOffsets(ImmutableDictionary<string, long> pendingOffsets);

        void AddConsensusCheck(ConsensusCheck check);
        
        void RemoveConsensusCheck(string id);
    }
}