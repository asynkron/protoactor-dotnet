// -----------------------------------------------------------------------
// <copyright file="IdentityMetrics.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Proto.Metrics;
using Ubiquitous.Metrics;

namespace Proto.Cluster.Identity
{
    public class IdentityMetrics
    {
        public IdentityMetrics(ProtoMetrics metrics)
        {
            WaitForActivationHistogram = metrics.CreateHistogram("protocluster_identity_wait_for_activation_duration_in_seconds", "", "id", "address", "clusterkind");
            GetWithGlobalLockHistogram = metrics.CreateHistogram("protocluster_identity_get_with_global_lock_duration_in_seconds", "", "id", "address", "clusterkind");
            TryAcquireLockHistogram = metrics.CreateHistogram("protocluster_identity_try_aquire_lock_duration_in_seconds", "", "id", "address", "clusterkind");
        }

        public readonly IHistogramMetric WaitForActivationHistogram;
        public readonly IHistogramMetric GetWithGlobalLockHistogram;
        public readonly IHistogramMetric TryAcquireLockHistogram;
    }
}