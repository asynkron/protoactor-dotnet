using System;

namespace Proto.Cluster.Consul
{
    public sealed record ConsulProviderConfig
    {
        /// <summary>
        ///     Default value is 3 seconds
        /// </summary>
        public TimeSpan ServiceTtl { get; init; } = TimeSpan.FromSeconds(10);

        public ConsulProviderConfig WithServiceTtl(TimeSpan serviceTtl) =>
            this with {ServiceTtl = serviceTtl};

        /// <summary>
        ///     Default value is 1 second
        /// </summary>
        public TimeSpan RefreshTtl { get; init; } = TimeSpan.FromSeconds(1);
        
        public ConsulProviderConfig WithRefreshTtl(TimeSpan refreshTtl) =>
            this with {RefreshTtl = refreshTtl};

        /// <summary>
        ///     Default value is 10 seconds
        /// </summary>
        public TimeSpan DeregisterCritical { get; init; } = TimeSpan.FromSeconds(30);
        
        public ConsulProviderConfig WithDeregisterCritical(TimeSpan deregisterCritical) =>
            this with {DeregisterCritical = deregisterCritical};

        /// <summary>
        ///     Default value is 20 seconds
        /// </summary>
        public TimeSpan BlockingWaitTime { get; init; } = TimeSpan.FromSeconds(20);
        
        public ConsulProviderConfig WithBlockingWaitTime(TimeSpan blockingWaitTime) =>
            this with {BlockingWaitTime = blockingWaitTime};
    }
}