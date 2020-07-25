using System;

namespace Proto.Cluster.Consul
{
    internal class QueryOptions
    {
        public ulong WaitIndex { get; set; }
        public TimeSpan WaitTime { get; set; }
    }
}