using System;

namespace Proto.Cluster.Consul
{
    public class ConsulProviderOptions
    {
        /// <summary>
        /// Default value is 3 seconds
        /// </summary>
        public TimeSpan ServiceTtl { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Default value is 1 second
        /// </summary>
        public TimeSpan RefreshTtl { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Default value is 10 seconds
        /// </summary>
        public TimeSpan DeregisterCritical { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Default value is 20 seconds
        /// </summary>
        public TimeSpan BlockingWaitTime { get; set; } = TimeSpan.FromSeconds(20);
    }
}