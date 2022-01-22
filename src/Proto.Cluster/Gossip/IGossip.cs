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
    public interface IGossip : IGossipStateStore, IGossipConsensusChecker, IGossipCore
    {

    }

    public interface IGossipCore
    {
        Task UpdateClusterTopology(ClusterTopology clusterTopology);

        IReadOnlyCollection<GossipUpdate> MergeState(GossipState remoteState);

        void GossipState(Action<Member, InstanceLogger?> gossipToMember);

        bool TryGetMemberState(string memberId, out ImmutableDictionary<string, long> pendingOffsets, out GossipState memberState);

        void CommitPendingOffsets(ImmutableDictionary<string, long> pendingOffsets);
    }

    public interface IGossipConsensusChecker
    {
        void AddConsensusCheck(ConsensusCheck check);
        
        void RemoveConsensusCheck(string id);
    }

    public interface IGossipStateStore
    {
        ImmutableDictionary<string, Any> GetState(string key);
        
        void SetState(string key, IMessage value);
    }
}