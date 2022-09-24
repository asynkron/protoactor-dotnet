// -----------------------------------------------------------------------
// <copyright file="ClusterMetrics.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.Metrics;
using Proto.Metrics;

namespace Proto.Cluster.Metrics;

public static class ClusterMetrics
{
    public static readonly Histogram<double> ClusterActorSpawnDuration =
        ProtoMetrics.Meter.CreateHistogram<double>("protocluster_virtualactor_spawn_duration", "seconds",
            "Time it takes to spawn a virtual actor"
        );

    public static readonly Histogram<double> ClusterRequestDuration = ProtoMetrics.Meter.CreateHistogram<double>(
        "protocluster_virtualactor_requestasync_duration", "seconds",
        "Cluster request duration"
    );

    public static readonly Counter<long> ClusterRequestRetryCount = ProtoMetrics.Meter.CreateCounter<long>(
        "protocluster_virtualactor_requestasync_retry_count",
        description: "Number of retries after failed cluster requests"
    );

    public static readonly Histogram<double> ClusterResolvePidDuration =
        ProtoMetrics.Meter.CreateHistogram<double>("protocluster_resolve_pid_duration", "seconds",
            "Time it takes to resolve a pid"
        );

    public static readonly ObservableGaugeWrapper<long> VirtualActorsCount = new();
    public static readonly ObservableGaugeWrapper<long> ClusterMembersCount = new();

    static ClusterMetrics()
    {
        ProtoMetrics.Meter.CreateObservableGauge(
            "protocluster_virtualactors",
            VirtualActorsCount.Observe,
            description: "Number of active virtual actors on this node"
        );

        ProtoMetrics.Meter.CreateObservableGauge(
            "protocluster_members_count",
            ClusterMembersCount.Observe,
            description: "Number of cluster members as seen by this node"
        );
    }
}