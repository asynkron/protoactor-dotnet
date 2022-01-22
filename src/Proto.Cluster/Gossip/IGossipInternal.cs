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

        ImmutableDictionary<string, Any> GetGossipStateKey(GetGossipStateRequest getState);

        //TODO: should this not be part of MemberList instead? it's not related to the gossip logic
        //also, why don't GossipRequest have MemberId in request?
        Member? TryGetSenderMember(string senderAddress);

        IReadOnlyCollection<GossipUpdate> MergeState(GossipState remoteState);

        void SetState(string key, IMessage value);

        void SendState(Action<Member, InstanceLogger?> sendGossipForMember);

        bool TryGetMemberState(Member member, out ImmutableDictionary<string, long> pendingOffsets, out GossipState stateForMember);

        void CommitPendingOffsets(ImmutableDictionary<string, long> pendingOffsets);

        void AddConsensusCheck(ConsensusCheck check);
        
        void RemoveConsensusCheck(string id);
    }
}