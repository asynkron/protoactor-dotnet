// -----------------------------------------------------------------------
// <copyright file="MemberRing.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using Proto.Router;

namespace Proto.Cluster.Partition;

public class MemberHashRing : HashRing<Member>

{
    public MemberHashRing(IReadOnlyCollection<Member> nodes) : base(nodes, member => member.Address, MurmurHash2.Hash,
        50)
    {
    }

    public string GetOwnerMemberByIdentity(ClusterIdentity clusterIdentity) =>
        GetNode(clusterIdentity.Identity)?.Address ?? "";

    public string GetOwnerMemberByIdentity(string identity) => GetNode(identity)?.Address ?? "";
}