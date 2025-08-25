using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Proto.Cluster.Gossip;
using Proto.Cluster.Identity;
using Proto.Diagnostics;
using Proto.Remote;

namespace Proto.Cluster;

/// <summary>
///     Provides diagnostic information about the current cluster instance.
/// </summary>
internal class ClusterDiagnostics
{
    private readonly Cluster _cluster;

    public ClusterDiagnostics(Cluster cluster) => _cluster = cluster;

    public async Task<DiagnosticsEntry[]> GetDiagnostics()
    {
        var res = new List<DiagnosticsEntry>();

        var now = new DiagnosticsEntry("Cluster", "Local Time", DateTimeOffset.UtcNow);
        res.Add(now);

        var blocked = new DiagnosticsEntry("Cluster", "Blocked",
            _cluster.System.Remote().BlockList.BlockedMembers.ToArray());
        res.Add(blocked);

        var topologyState = await _cluster.Gossip.GetState<ClusterTopology>(GossipKeys.Topology).ConfigureAwait(false);
        var topology = new DiagnosticsEntry("Cluster", "Topology", topologyState);
        res.Add(topology);

        var h = await _cluster.Gossip.GetStateEntry(GossipKeys.Heartbeat).ConfigureAwait(false);
        var heartbeats = h.Select(heartbeat => new DiagnosticsMemberHeartbeat(heartbeat.Key,
                heartbeat.Value.Value.Unpack<MemberHeartbeat>(), heartbeat.Value.LocalTimestamp))
            .ToArray();
        var heartbeat = new DiagnosticsEntry("Cluster", "Heartbeat", heartbeats);
        res.Add(heartbeat);

        var idlookup = await _cluster.IdentityLookup.GetDiagnostics().ConfigureAwait(false);
        res.AddRange(idlookup);

        var provider = await _cluster.Provider.GetDiagnostics().ConfigureAwait(false);
        res.AddRange(provider);

        return res.ToArray();
    }
}
