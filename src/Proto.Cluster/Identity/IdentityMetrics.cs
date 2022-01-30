// -----------------------------------------------------------------------
// <copyright file="IdentityMetrics.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Diagnostics.Metrics;
using Proto.Metrics;

namespace Proto.Cluster.Identity
{
    public static class IdentityMetrics
    {
        public static readonly Histogram<double> WaitForActivationDuration = ProtoMetrics.Meter.CreateHistogram<double>(
            "protocluster_identity_wait_for_activation_duration", "seconds", "Time spent waiting for activation of cluster kind to complete"
        );

        public static readonly Histogram<double> GetWithGlobalLockDuration =
            ProtoMetrics.Meter.CreateHistogram<double>("protocluster_identity_get_with_global_lock_duration", "seconds", "");

        public static readonly Histogram<double> TryAcquireLockDuration = ProtoMetrics.Meter.CreateHistogram<double>(
            "protocluster_identity_try_acquire_lock_duration", "seconds",
            "Time spent trying to acquire the global lock for cluster kind from identity storage"
        );
    }
}