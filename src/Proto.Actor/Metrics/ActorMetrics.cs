// -----------------------------------------------------------------------
// <copyright file="ActorMetrics.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics.Metrics;

namespace Proto.Metrics
{
    public class ActorMetrics
    {
        public readonly Counter<long> ActorFailureCount;
        public readonly Histogram<long> ActorMailboxLength;
        public readonly Histogram<double> ActorMessageReceiveDuration;
        public readonly Counter<long> ActorRestartedCount;

        //Actors
        public readonly Counter<long> ActorSpawnCount;
        public readonly Counter<long> ActorStoppedCount;

        //Deadletters
        public readonly Counter<long> DeadletterCount;
        public readonly Counter<long> FuturesCompletedCount;

        //Futures
        public readonly Counter<long> FuturesStartedCount;
        public readonly Counter<long> FuturesTimedOutCount;

        //Threadpool
        public readonly Histogram<double> ThreadPoolLatency;

        public ActorMetrics(ProtoMetrics metrics)
        {
            ThreadPoolLatency = metrics.CreateHistogram<double>(
                "protoactor_threadpool_latency_duration",
                "seconds",
                "Latency of the thread pool measured as time required to spawn a new task"
            );

            DeadletterCount =
                metrics.CreateCounter<long>("protoactor_deadletter_count", description: "Number of messages sent to deadletter process");
            ActorSpawnCount = metrics.CreateCounter<long>("protoactor_actor_spawn_count", description: "Number of spawned actor instances");
            ActorStoppedCount = metrics.CreateCounter<long>("protoactor_actor_stopped_count", description: "Number of stopped actors");
            ActorRestartedCount = metrics.CreateCounter<long>("protoactor_actor_restarted_count", description: "Number of restarted actors");
            ActorFailureCount =
                metrics.CreateCounter<long>("protoactor_actor_failure_count", description: "Number of detected and escalated failures");

            ActorMailboxLength = metrics.CreateHistogram<long>("protoactor_actor_mailbox_length",
                description: "Histogram of queue lengths across all actor instances"
            );

            ActorMessageReceiveDuration =
                metrics.CreateHistogram<double>("protoactor_actor_messagereceive_duration", "seconds", "Time spent in actor's receive handler");

            FuturesStartedCount = metrics.CreateCounter<long>("protoactor_future_started_count", description: "Number of started futures");
            FuturesTimedOutCount = metrics.CreateCounter<long>("protoactor_future_timedout_count", description: "Number of futures that timed out");
            FuturesCompletedCount = metrics.CreateCounter<long>("protoactor_future_completed_count", description: "Number of completed futures");
        }
    }
}