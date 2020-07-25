using System;

namespace Proto.Cluster.Testing
{
    public class QueryOptions
    {
        public ulong WaitIndex { get; set; }
        public TimeSpan WaitTime { get; set; }
    }
}