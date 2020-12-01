// -----------------------------------------------------------------------
// <copyright file="ConsulProviderConfig.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;

namespace Proto.Cluster.Consul
{
    public sealed record ConsulProviderConfig
    {
        /// <summary>
        ///     Default value is 3 seconds
        /// </summary>
        public TimeSpan ServiceTtl { get; init; } = TimeSpan.FromSeconds(10);

        /// <summary>
        ///     Default value is 1 second
        /// </summary>
        public TimeSpan RefreshTtl { get; init; } = TimeSpan.FromSeconds(1);

        /// <summary>
        ///     Default value is 10 seconds
        /// </summary>
        public TimeSpan DeregisterCritical { get; init; } = TimeSpan.FromSeconds(30);

        /// <summary>
        ///     Default value is 20 seconds
        /// </summary>
        public TimeSpan BlockingWaitTime { get; init; } = TimeSpan.FromSeconds(20);

        public ConsulProviderConfig WithServiceTtl(TimeSpan serviceTtl) =>
            this with {ServiceTtl = serviceTtl};

        public ConsulProviderConfig WithRefreshTtl(TimeSpan refreshTtl) =>
            this with {RefreshTtl = refreshTtl};

        public ConsulProviderConfig WithDeregisterCritical(TimeSpan deregisterCritical) =>
            this with {DeregisterCritical = deregisterCritical};

        public ConsulProviderConfig WithBlockingWaitTime(TimeSpan blockingWaitTime) =>
            this with {BlockingWaitTime = blockingWaitTime};
    }
}