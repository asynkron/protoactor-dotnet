// -----------------------------------------------------------------------
// <copyright file="ClusterMetrics.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Proto.Metrics;
using Ubiquitous.Metrics;

namespace Proto.Cluster.Metrics
{
    public class ClusterMetrics
    {
        public readonly IGaugeMetric ClusterActorGauge;
        public readonly IHistogramMetric ClusterActorSpawnHistogram;
        public readonly IHistogramMetric ClusterRequestHistogram;
        public readonly ICountMetric ClusterRequestRetryCount;
        public readonly IGaugeMetric ClusterTopologyEventGauge;
        public readonly IHistogramMetric ClusterResolvePidHistogram;

        public ClusterMetrics(ProtoMetrics metrics)
        {
            ClusterActorGauge = metrics.CreateGauge("protocluster_virtualactors", "", "id", "address", "clusterkind");

            ClusterActorSpawnHistogram =
                metrics.CreateHistogram("protocluster_virtualactor_spawn_duration_seconds", "", "id", "address", "clusterkind");

            ClusterRequestHistogram = metrics.CreateHistogram("protocluster_virtualactor_requestasync_duration_seconds", "", "id", "address",
                "clusterkind", "messagetype", "pidsource"
            );

            ClusterRequestRetryCount = metrics.CreateCount("protocluster_virtualactor_requestasync_retry_count", "", "id", "address", "clusterkind",
                "messagetype"
            );

            ClusterTopologyEventGauge = metrics.CreateGauge("protocluster_topology_events", "", "id", "address", "membershiphashcode");
            
            ClusterResolvePidHistogram =
                metrics.CreateHistogram("protocluster_resolve_pid_duration_seconds", "", "id", "address", "clusterkind");

        }
    }
}