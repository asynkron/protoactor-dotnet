// -----------------------------------------------------------------------
// <copyright file="ClusterMetrics.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Proto.Metrics;
using Ubiquitous.Metrics;
using Ubiquitous.Metrics.Labels;

namespace Proto.Cluster.Metrics
{
    public class ClusterMetrics
    {
        public ClusterMetrics(ProtoMetrics metrics)
        {
            ClusterActorCount = metrics.CreateCount("protocluster_virtualactor_count", "", "id","address","clusterkind");
            
            ClusterActorSpawnHistogram= metrics.CreateHistogram("protocluster_virtualactor_spawn_duration_seconds" , "", "id","address","clusterkind");
            
            ClusterRequestHistogram= metrics.CreateHistogram("protocluster_virtualactor_requestasync_duration_seconds" , "", "id","address","clusterkind","messagetype");

            ClusterRequestRetryCount = metrics.CreateCount("protocluster_virtualactor_requestasync_retry_count", "","id","address","clusterkind","messagetype","pidsource");
            
            ClusterTopologyEventGauge = metrics.CreateGauge("protocluster_topology_events", "","id","address");
        }

        public readonly ICountMetric ClusterActorCount;
        public readonly IHistogramMetric ClusterActorSpawnHistogram;
        public readonly IHistogramMetric ClusterRequestHistogram;
        public readonly ICountMetric ClusterRequestRetryCount;
        public readonly IGaugeMetric ClusterTopologyEventGauge;
    }
}