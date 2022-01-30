// -----------------------------------------------------------------------
// <copyright file="PartitionMemberSelector.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Immutable;

namespace Proto.Cluster.Partition
{
    //this class is responsible for translating between Identity->member
    //this is the key algorithm for the distributed hash table
    class PartitionMemberSelector
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

        public (string owner, ulong topologyHash) GetIdentityOwner(string key)
        {
            lock (_lock) return (_rdv.GetOwnerMemberByIdentity(key), _topologyHash);
        }
    }
}