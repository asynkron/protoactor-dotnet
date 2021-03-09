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
            SpawnCount = metrics.CreateCount(nameof(SpawnCount), new string[] { });
        }

        public readonly ICountMetric SpawnCount;

    }
}