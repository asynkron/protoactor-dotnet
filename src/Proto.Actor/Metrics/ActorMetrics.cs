// -----------------------------------------------------------------------
// <copyright file="ActorMetrics.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Proto.Metrics
{
    public class ActorMetrics
    {
        public ActorMetrics(Metrics metrics)
        {
            const string prefix = "proto_actor_";

            DeadletterCount = metrics.CreateCount(prefix + nameof(DeadletterCount), new string[] { });
            
            ActorSpawnCount = metrics.CreateCount(prefix + nameof(ActorSpawnCount), new string[] { });
            ActorStoppedCount = metrics.CreateCount(prefix + nameof(ActorStoppedCount), new string[] { });
            ActorRestartedCount = metrics.CreateCount(prefix + nameof(ActorRestartedCount), new string[] { });
            ActorFailureCount = metrics.CreateCount(prefix + nameof(ActorFailureCount), new string[] { });

            FuturesStartedCount = metrics.CreateCount(prefix + nameof(FuturesStartedCount), new string[] { });
            FuturesTimedOutCount = metrics.CreateCount(prefix + nameof(FuturesTimedOutCount), new string[] { });
            FuturesCompletedCount = metrics.CreateCount(prefix + nameof(FuturesCompletedCount), new string[] { });
        }

        //Deadletters
        public readonly ICountMetric DeadletterCount; //done
        
        //Actors
        public readonly ICountMetric ActorSpawnCount;     //done
        public readonly ICountMetric ActorStoppedCount;   //done
        public readonly ICountMetric ActorRestartedCount; //done
        public readonly ICountMetric ActorFailureCount;   //done

        //Futures
        public readonly ICountMetric FuturesStartedCount;   //done
        public readonly ICountMetric FuturesTimedOutCount;  //done
        public readonly ICountMetric FuturesCompletedCount; //done
    }
}