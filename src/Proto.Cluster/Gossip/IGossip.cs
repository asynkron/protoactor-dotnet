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
    internal interface IGossip : IGossipStateStore, IGossipConsensusChecker, IGossipCore
    {

    }

    internal interface IGossipCore
    {
        Task UpdateClusterTopology(ClusterTopology clusterTopology);

        ImmutableList<GossipUpdate> MergeState(GossipState remoteState);

        void GossipState(Action<Member, InstanceLogger?, MemberStateDelta> gossipToMember);

        MemberStateDelta GetMemberStateDelta(string memberId);

        void CommitPendingOffsets(ImmutableDictionary<string, long> pendingOffsets);
    }

    internal interface IGossipConsensusChecker
    {
        void AddConsensusCheck(ConsensusCheck check);
        
        void RemoveConsensusCheck(string id);
    }

    internal interface IGossipStateStore
    {
        ImmutableDictionary<string, Any> GetState(string key);
        
        void SetState(string key, IMessage value);
    }
}