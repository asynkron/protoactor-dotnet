using System;
using System.Collections.Generic;
using System.Linq;

namespace Proto.Cluster.Events
{
    public class ClusterTopologyEvent
    {
        public ClusterTopologyEvent(IEnumerable<MemberInfo> members)
        {
            Members = members?.ToArray() ?? throw new ArgumentNullException(nameof(members));
        }

        public IReadOnlyCollection<MemberInfo> Members { get; }
    }
}