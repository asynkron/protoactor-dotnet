// -----------------------------------------------------------------------
// <copyright file="ClusterMetrics.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Diagnostics.Metrics;
using Proto.Metrics;

namespace Proto.Cluster.Metrics
{
    public class ClusterMetrics
    {
        public readonly Histogram<double> ClusterActorSpawnDuration;
        public readonly Histogram<double> ClusterRequestDuration;
        public readonly Counter<long> ClusterRequestRetryCount;
        public readonly Histogram<double> ClusterResolvePidDuration;

        public ObservableGaugeWrapper<long> VirtualActorsCount;
        public ObservableGaugeWrapper<long> ClusterMembersCount;

        public ClusterMetrics(ProtoMetrics metrics)
        {
            ClusterActorSpawnDuration =
                metrics.CreateHistogram<double>("protocluster_virtualactor_spawn_duration", unit: "seconds", description: "Time it takes to spawn a virtual actor");

            ClusterRequestDuration = metrics.CreateHistogram<double>("protocluster_virtualactor_requestasync_duration", unit: "seconds",
                description: "Cluster request duration"
            );

            ClusterRequestRetryCount = metrics.CreateCounter<long>("protocluster_virtualactor_requestasync_retry_count",
                description: "Number of retries after failed cluster requests"
            );

            ClusterResolvePidDuration =
                metrics.CreateHistogram<double>("protocluster_resolve_pid_duration", unit: "seconds",
                    description: "Time it takes to resolve a pid"
                );

            VirtualActorsCount = new ObservableGaugeWrapper<long>();
            metrics.CreateObservableGauge(
                "protocluster_virtualactors",
                VirtualActorsCount.Observe,
                description: "Number of active virtual actors on this node"
            );

            ClusterMembersCount = new ObservableGaugeWrapper<long>();
            metrics.CreateObservableGauge(
                "protocluster_members_count",
                ClusterMembersCount.Observe,
                description: "Number of cluster members as seen by this node"
            );
        }
    }
}