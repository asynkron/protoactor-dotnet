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
            const string prefix = "proto_actor_";

            DeadletterCount = metrics.CreateCount(prefix + nameof(DeadletterCount), "", "message-type");
            
            ActorSpawnCount = metrics.CreateCount(prefix + nameof(ActorSpawnCount), "", "actor-type");
            ActorStoppedCount = metrics.CreateCount(prefix + nameof(ActorStoppedCount), "", "actor-type");
            ActorRestartedCount = metrics.CreateCount(prefix + nameof(ActorRestartedCount), "", "actor-type");
            ActorFailureCount = metrics.CreateCount(prefix + nameof(ActorFailureCount), "", "actor-type");
            ActorMessageReceiveHistogram = metrics.CreateHistogram(prefix + nameof(ActorMessageReceiveHistogram), "", "actor-type");

            FuturesStartedCount = metrics.CreateCount(prefix + nameof(FuturesStartedCount), "");
            FuturesTimedOutCount = metrics.CreateCount(prefix + nameof(FuturesTimedOutCount), "");
            FuturesCompletedCount = metrics.CreateCount(prefix + nameof(FuturesCompletedCount), "");
        }

        //Deadletters
        public readonly ICountMetric DeadletterCount; //done
        
        //Actors
        public readonly ICountMetric ActorSpawnCount;     //done
        public readonly ICountMetric ActorStoppedCount;   //done
        public readonly ICountMetric ActorRestartedCount; //done
        public readonly ICountMetric ActorFailureCount;   //done
        public readonly IHistogramMetric ActorMessageReceiveHistogram;   //done

        //Futures
        public readonly ICountMetric FuturesStartedCount;   //done
        public readonly ICountMetric FuturesTimedOutCount;  //done
        public readonly ICountMetric FuturesCompletedCount; //done
    }
}