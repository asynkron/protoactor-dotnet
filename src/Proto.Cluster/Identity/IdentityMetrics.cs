// -----------------------------------------------------------------------
// <copyright file="IdentityMetrics.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Diagnostics.Metrics;
using Proto.Metrics;

namespace Proto.Cluster.Identity;

public static class IdentityMetrics
{
    public static void RecordActivationRequestSent(ActorSystem system, string kind)
    {
        ActivationRequestSentCount.Add(1,
            new KeyValuePair<string, object?>("id", system.Id),
            new KeyValuePair<string, object?>("address", system.Address),
            new KeyValuePair<string, object?>("clusterkind", kind));
    }

    public static void RecordActivationRequestReceived(ActorSystem system, string kind)
    {
        ActivationRequestReceivedCount.Add(1,
            new KeyValuePair<string, object?>("id", system.Id),
            new KeyValuePair<string, object?>("address", system.Address),
            new KeyValuePair<string, object?>("clusterkind", kind));
    }

    public static readonly Histogram<double> WaitForActivationDuration = ProtoMetrics.Meter.CreateHistogram<double>(
        "protocluster_identity_wait_for_activation_duration", "seconds",
        "Time spent waiting for activation of cluster kind to complete"
    );

    public static readonly Histogram<double> GetWithGlobalLockDuration =
        ProtoMetrics.Meter.CreateHistogram<double>("protocluster_identity_get_with_global_lock_duration", "seconds",
            "");

    public static readonly Histogram<double> TryAcquireLockDuration = ProtoMetrics.Meter.CreateHistogram<double>(
        "protocluster_identity_try_acquire_lock_duration", "seconds",
        "Time spent trying to acquire the global lock for cluster kind from identity storage"
    );

    public static readonly Counter<long> ActivationRequestSentCount = ProtoMetrics.Meter.CreateCounter<long>(
        "protocluster_identity_activation_request_sent_count",
        description: "Number of activation requests sent by identity lookup providers"
    );

    public static readonly Counter<long> ActivationRequestReceivedCount = ProtoMetrics.Meter.CreateCounter<long>(
        "protocluster_activator_activation_request_received_count",
        description: "Number of activation requests received by activation actors"
    );

    public static readonly Counter<long> ActivationRequestForwardedCount = ProtoMetrics.Meter.CreateCounter<long>(
        "protocluster_activator_activation_request_forwarded_count",
        description: "Number of activation requests forwarded by activation actors"
    );
}
