// -----------------------------------------------------------------------
// <copyright file="ActorMetrics.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Ubiquitous.Metrics;

namespace Proto.Metrics
{
    public class ActorMetrics
    {
        public readonly ICountMetric ActorFailureCount; //done
        public readonly IGaugeMetric ActorMailboxLength;
        public readonly IHistogramMetric ActorMessageReceiveHistogram; //done
        public readonly ICountMetric ActorRestartedCount;              //done

        //Actors
        public readonly ICountMetric ActorSpawnCount;   //done
        public readonly ICountMetric ActorStoppedCount; //done

        //Deadletters
        public readonly ICountMetric DeadletterCount;       //done
        public readonly ICountMetric FuturesCompletedCount; //done

        //Futures
        public readonly ICountMetric FuturesStartedCount;  //done
        public readonly ICountMetric FuturesTimedOutCount; //done

        //Threadpool
        public readonly IHistogramMetric ThreadPoolLatencyHistogram; //done

        public ActorMetrics(ProtoMetrics metrics)
        {
            ThreadPoolLatencyHistogram = metrics.CreateHistogram("protoactor_threadpool_latency_duration_seconds", "", "id", "address");
            DeadletterCount = metrics.CreateCount("protoactor_deadletter_count", "", "id", "address", "messagetype");
            ActorSpawnCount = metrics.CreateCount("protoactor_actor_spawn_count", "", "id", "address", "actortype");
            ActorStoppedCount = metrics.CreateCount("protoactor_actor_stopped_count", "", "id", "address", "actortype");
            ActorRestartedCount = metrics.CreateCount("protoactor_actor_restarted_count", "", "id", "address", "actortype");
            ActorFailureCount = metrics.CreateCount("protoactor_actor_failure_count", "", "id", "address", "actortype");

            ActorMailboxLength = metrics.CreateGauge("protoactor_actor_mailbox_length", "", "id", "address", "actortype");

            ActorMessageReceiveHistogram = metrics.CreateHistogram("protoactor_actor_messagereceive_duration_seconds", "", "id", "address",
                "actortype", "messagetype"
            );
            FuturesStartedCount = metrics.CreateCount("protoactor_future_started_count", "", "id", "address");
            FuturesTimedOutCount = metrics.CreateCount("protoactor_future_timedout_count", "", "id", "address");
            FuturesCompletedCount = metrics.CreateCount("protoactor_future_completed_count", "", "id", "address");
        }
    }
}