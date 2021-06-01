// -----------------------------------------------------------------------
// <copyright file="ClusterContextConfiguration.cs" company="Asynkron AB">
//      Copyright (C) 2015-$CURRENT_YEAR$ Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace Proto.Cluster
{
    public record ClusterContextConfig
    {
        public ClusterContextConfig()
        {
            ActorRequestTimeout = TimeSpan.FromSeconds(5);
            MaxNumberOfEventsInRequestLogThrottlePeriod = 3;
            RequestLogThrottlePeriod = TimeSpan.FromSeconds(2);
        }

        public TimeSpan ActorRequestTimeout { get; init; }
        public TimeSpan RequestLogThrottlePeriod { get; init; }
        public int MaxNumberOfEventsInRequestLogThrottlePeriod { get; init; }
    }

    public static class ClusterConfigExtensions
    {
        public static ClusterContextConfig ToClusterContextConfig(this ClusterConfig clusterConfig)
            => new()
            {
                ActorRequestTimeout = clusterConfig.ActorRequestTimeout,
                MaxNumberOfEventsInRequestLogThrottlePeriod = clusterConfig.MaxNumberOfEventsInRequestLogThrottlePeriod,
                RequestLogThrottlePeriod = clusterConfig.RequestLogThrottlePeriod
            };
    }
}