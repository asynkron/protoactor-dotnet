using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using Proto.Cluster.Metrics;

namespace Proto.Cluster;

/// <summary>
///     Handles registration of cluster metrics observers.
/// </summary>
internal class ClusterMetricsCollector
{
    private readonly Cluster _cluster;
    private Func<IEnumerable<Measurement<long>>>? _clusterMembersObserver;
    private Func<IEnumerable<Measurement<long>>>? _clusterKindObserver;

    public ClusterMetricsCollector(Cluster cluster) => _cluster = cluster;

    public void RegisterMemberCountObserver()
    {
        if (!_cluster.System.Metrics.Enabled) return;

        _clusterMembersObserver = () => new[]
        {
            new Measurement<long>(_cluster.MemberList.GetAllMembers().Length,
                new KeyValuePair<string, object?>("id", _cluster.System.Id),
                new KeyValuePair<string, object?>("address", _cluster.System.Address))
        };

        ClusterMetrics.ClusterMembersCount.AddObserver(_clusterMembersObserver);
    }

    public void RegisterClusterKindObserver(Dictionary<string, ActivatedClusterKind> clusterKinds)
    {
        if (!_cluster.System.Metrics.Enabled) return;

        _clusterKindObserver = () => clusterKinds.Values.Select(ck =>
            new Measurement<long>(ck.Count,
                new KeyValuePair<string, object?>("id", _cluster.System.Id),
                new KeyValuePair<string, object?>("address", _cluster.System.Address),
                new KeyValuePair<string, object?>("clusterkind", ck.Name))
        );

        ClusterMetrics.VirtualActorsCount.AddObserver(_clusterKindObserver);
    }

    public void RemoveObservers()
    {
        if (_clusterKindObserver != null)
        {
            ClusterMetrics.VirtualActorsCount.RemoveObserver(_clusterKindObserver);
            _clusterKindObserver = null;
        }

        if (_clusterMembersObserver != null)
        {
            ClusterMetrics.ClusterMembersCount.RemoveObserver(_clusterMembersObserver);
            _clusterMembersObserver = null;
        }
    }
}
