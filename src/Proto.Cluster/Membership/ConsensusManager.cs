// -----------------------------------------------------------------------
// <copyright file="ConsensusManager.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using Proto.Cluster.Gossip;

namespace Proto.Cluster;

/// <summary>
///     Encapsulates topology consensus handling for a <see cref="MemberList" />.
///     Moving this logic out of <see cref="MemberList" /> keeps the type focused on membership bookkeeping.
/// </summary>
internal class ConsensusManager
{
    private readonly Cluster _cluster;
    private IConsensusHandle<ulong>? _topologyConsensus;

    internal ConsensusManager(Cluster cluster) => _cluster = cluster;

    /// <summary>
    ///     Initializes the topology consensus check used to ensure all members agree on the current cluster topology.
    /// </summary>
    internal void InitializeTopologyConsensus() =>
        _topologyConsensus =
            _cluster.Gossip.RegisterConsensusCheck<ClusterTopology, ulong>(GossipKeys.Topology,
                topology => topology.TopologyHash);

    /// <summary>
    ///     Attempts to retrieve the current consensus state for the cluster topology.
    /// </summary>
    /// <param name="ct">Cancellation token controlling how long to wait for consensus.</param>
    internal Task<(bool consensus, ulong topologyHash)> TopologyConsensus(CancellationToken ct) =>
        _topologyConsensus?.TryGetConsensus(ct) ??
        Task.FromResult<(bool consensus, ulong topologyHash)>(default);
}

