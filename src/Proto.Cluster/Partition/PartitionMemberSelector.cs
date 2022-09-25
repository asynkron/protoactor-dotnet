// -----------------------------------------------------------------------
// <copyright file="PartitionMemberSelector.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;
using System.Threading;

namespace Proto.Cluster.Partition;

//this class is responsible for translating between Identity->member
//this is the key algorithm for the distributed hash table
internal class PartitionMemberSelector
{
    private State _state = new(new MemberHashRing(ImmutableList<Member>.Empty), 0);

    public void Update(Member[] members, ulong topologyHash) =>
        Interlocked.Exchange(ref _state, new State(new MemberHashRing(members), topologyHash));

    public (string owner, ulong topologyHash) GetIdentityOwner(string key)
    {
        var (memberHashRing, topologyHash) = _state;
        var owner = memberHashRing.GetOwnerMemberByIdentity(key);

        return (owner, topologyHash);
    }

    private record State(MemberHashRing Rdv, ulong TopologyHash);
}