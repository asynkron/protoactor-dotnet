// -----------------------------------------------------------------------
// <copyright file="ActorMetrics.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.Metrics;

namespace Proto.Metrics;

public static class ActorMetrics
{
    //Actors
    public static readonly Counter<long> ActorFailureCount =
        ProtoMetrics.Meter.CreateCounter<long>("protoactor_actor_failure_count",
            description: "Number of detected and escalated failures");

    public static readonly Histogram<long> ActorMailboxLength = ProtoMetrics.Meter.CreateHistogram<long>(
        "protoactor_actor_mailbox_length",
        description: "Histogram of queue lengths across all actor instances"
    );

    public static readonly Histogram<double> ActorMessageReceiveDuration =
        ProtoMetrics.Meter.CreateHistogram<double>("protoactor_actor_messagereceive_duration", "seconds",
            "Time spent in actor's receive handler"
        );

    public static readonly Counter<long> ActorRestartedCount =
        ProtoMetrics.Meter.CreateCounter<long>("protoactor_actor_restarted_count",
            description: "Number of restarted actors");

    public static readonly Counter<long> ActorSpawnCount =
        ProtoMetrics.Meter.CreateCounter<long>("protoactor_actor_spawn_count",
            description: "Number of spawned actor instances");

    public static readonly Counter<long> ActorStoppedCount =
        ProtoMetrics.Meter.CreateCounter<long>("protoactor_actor_stopped_count",
            description: "Number of stopped actors");

    //Deadletters
    public static readonly Counter<long> DeadletterCount =
        ProtoMetrics.Meter.CreateCounter<long>("protoactor_deadletter_count",
            description: "Number of messages sent to deadletter process");

    public static readonly Counter<long> FuturesCompletedCount =
        ProtoMetrics.Meter.CreateCounter<long>("protoactor_future_completed_count",
            description: "Number of completed futures");

    //Futures
    public static readonly Counter<long> FuturesStartedCount =
        ProtoMetrics.Meter.CreateCounter<long>("protoactor_future_started_count",
            description: "Number of started futures");

    public static readonly Counter<long> FuturesTimedOutCount =
        ProtoMetrics.Meter.CreateCounter<long>("protoactor_future_timedout_count",
            description: "Number of futures that timed out");

    //Threadpool
    public static readonly Histogram<double> ThreadPoolLatency = ProtoMetrics.Meter.CreateHistogram<double>(
        "protoactor_threadpool_latency_duration",
        "seconds",
        "Latency of the thread pool measured as time required to spawn a new task"
    );
}