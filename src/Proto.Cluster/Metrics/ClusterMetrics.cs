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
            const string prefix = "proto_cluster_";
            ClusterActorCount = metrics.CreateCount(prefix + nameof(ClusterActorCount), "", "cluster-kind");
            
            ClusterActorSpawnHistogram= metrics.CreateHistogram(prefix + nameof(ClusterActorSpawnHistogram), "", "cluster-kind");
        }

        public readonly ICountMetric ClusterActorCount;

        public readonly IHistogramMetric ClusterActorSpawnHistogram;
    }
}