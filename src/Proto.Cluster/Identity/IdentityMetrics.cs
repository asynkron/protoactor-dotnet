// -----------------------------------------------------------------------
// <copyright file="IdentityMetrics.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Diagnostics.Metrics;
using Proto.Metrics;

namespace Proto.Cluster.Identity
{
    public class IdentityMetrics
    {
        public IdentityMetrics(ProtoMetrics metrics)
        {
            WaitForActivationDuration = metrics.CreateHistogram<double>("protocluster_identity_wait_for_activation_duration", "seconds", "Time spent waiting for activation of cluster kind to complete");
            GetWithGlobalLockDuration = metrics.CreateHistogram<double>("protocluster_identity_get_with_global_lock_duration", "seconds", "");
            TryAcquireLockDuration = metrics.CreateHistogram<double>("protocluster_identity_try_aquire_lock_duration", "seconds", "Time spent trying to acquire the global lock for cluster kind from identity storage");
        }

        public readonly Histogram<double> WaitForActivationDuration;
        public readonly Histogram<double> GetWithGlobalLockDuration;
        public readonly Histogram<double> TryAcquireLockDuration;
    }
}