// -----------------------------------------------------------------------
// <copyright file="Extensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using Proto.Cluster.Gossip;

namespace Proto.Cluster.Partition
{
    internal static class Extensions
    {
        private const string ReadyForRebalanceKey = "reb:ready";
        private const string RebalanceCompletedKey = "reb:done";

        private static readonly Gossiper.ConsensusCheckBuilder<ulong> TopologyConsensus = Gossiper.ConsensusCheckBuilder<ulong>
            .Create<ClusterTopology>(GossipKeys.Topology, topology => topology.TopologyHash);

        private static readonly Gossiper.ConsensusCheckBuilder<ulong> ReadyForRebalance = TopologyConsensus
            .InConsensusWith<ReadyForRebalance>(ReadyForRebalanceKey, rebalance => rebalance.TopologyHash);

        private static readonly Gossiper.ConsensusCheckBuilder<ulong> RebalanceCompleted = TopologyConsensus
            .InConsensusWith<RebalanceCompleted>(RebalanceCompletedKey, rebalance => rebalance.TopologyHash);

        public static async Task<(bool consensus, T value)> WaitFor<T>(
            this Gossiper gossip,
            Gossiper.ConsensusCheckBuilder<T> check,
            TimeSpan maxWait,
            CancellationToken cancellationToken
        )
        {
            using var consensusCheck = gossip.RegisterConsensusCheck(check);

            return await consensusCheck.TryGetConsensus(maxWait, cancellationToken).ConfigureAwait(false);
        }

        public static Task<(bool consensus, ulong topologyHash)> WaitUntilInFlightActivationsAreCompleted(
            this Gossiper gossip,
            TimeSpan maxWait,
            CancellationToken cancellationToken
        ) => WaitFor(gossip, ReadyForRebalance, maxWait, cancellationToken);

        public static Task<(bool consensus, ulong topologyHash)> WaitUntilAllMembersCompletedRebalance(
            this Gossiper gossip,
            TimeSpan maxWait,
            CancellationToken cancellationToken
        ) => WaitFor(gossip, RebalanceCompleted, maxWait, cancellationToken);

        public static void SetInFlightActivationsCompleted(this Gossiper gossip, ulong topologyHash)
            => gossip.SetState(ReadyForRebalanceKey, new ReadyForRebalance {TopologyHash = topologyHash});

        public static void SetRebalanceCompleted(this Gossiper gossip, ulong topologyHash)
            => gossip.SetState(RebalanceCompletedKey, new RebalanceCompleted {TopologyHash = topologyHash});
    }
}