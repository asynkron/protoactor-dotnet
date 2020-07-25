using System;

namespace Proto.Cluster.Testing
{
    internal class QueryOptions
    {
        public ulong WaitIndex { get; set; }
        public TimeSpan WaitTime { get; set; }
    }
}