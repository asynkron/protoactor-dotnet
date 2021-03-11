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
        public ActorMetrics(ProtoMetrics metrics)
        {
            DeadletterCount = metrics.CreateCount("protoactor_deadletter_count", "", "messagetype");
            ActorSpawnCount = metrics.CreateCount("protoactor_actor_spawn_count", "", "actortype");
            ActorStoppedCount = metrics.CreateCount("protoactor_actor_stopped_count", "", "actortype");
            ActorRestartedCount = metrics.CreateCount("protoactor_actor_restarted_count", "", "actortype");
            ActorFailureCount = metrics.CreateCount("protoactor_actor_failure_count", "", "actortype");

            ActorMailboxLength = metrics.CreateGauge("protoactor_actor_mailbox_length", "", "actortype");
            
            ActorMessageReceiveHistogram = metrics.CreateHistogram("protoactor_actor_messagereceive_duration_seconds", "", "actortype","messagetype");
            FuturesStartedCount = metrics.CreateCount("protoactor_future_started_count","");
            FuturesTimedOutCount = metrics.CreateCount("protoactor_future_timedout_count", "");
            FuturesCompletedCount = metrics.CreateCount("protoactor_future_completed_count", "");
        }

        //Deadletters
        public readonly ICountMetric DeadletterCount; //done
        
        //Actors
        public readonly ICountMetric ActorSpawnCount;     //done
        public readonly ICountMetric ActorStoppedCount;   //done
        public readonly ICountMetric ActorRestartedCount; //done
        public readonly ICountMetric ActorFailureCount;   //done
        public readonly IHistogramMetric ActorMessageReceiveHistogram;   //done
        public readonly IGaugeMetric ActorMailboxLength;

        //Futures
        public readonly ICountMetric FuturesStartedCount;   //done
        public readonly ICountMetric FuturesTimedOutCount;  //done
        public readonly ICountMetric FuturesCompletedCount; //done
    }
}