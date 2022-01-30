// -----------------------------------------------------------------------
// <copyright file="PartitionMemberSelector.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Immutable;
using Proto.Cluster.Partition;

namespace Proto.Cluster.PartitionActivator
{
    //this class is responsible for translating between Identity->activator member
    //this is the key algorithm for the distributed hash table
    class PartitionActivatorSelector
    {
        private readonly object _lock = new();
        private MemberHashRing _rdv = new(ImmutableList<Member>.Empty);
        private ulong _topologyHash;

        public void Update(Member[] members, ulong topologyHash)
        {
            lock (_lock)
            {
                _rdv = new MemberHashRing(members);
                _topologyHash = topologyHash;
            }
        }

        public string GetOwner(ClusterIdentity key)
        {
            lock (_lock) return _rdv.GetOwnerMemberByIdentity(key);
        }
    }
}