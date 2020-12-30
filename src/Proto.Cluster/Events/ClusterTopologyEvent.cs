// -----------------------------------------------------------------------
// <copyright file="ClusterTopologyEvent.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;

namespace Proto.Cluster.Events
{
    public class ClusterTopologyEvent
    {
        public ClusterTopologyEvent(IEnumerable<Member> members) => Members = members?.ToArray() ?? throw new ArgumentNullException(nameof(members));

        public IReadOnlyCollection<Member> Members { get; }
    }
}