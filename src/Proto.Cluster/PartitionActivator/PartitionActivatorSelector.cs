// -----------------------------------------------------------------------
// <copyright file="PartitionActivatorSelector.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Proto.Cluster.PartitionActivator;

//this class is responsible for translating between Identity->activator member
//this is the key algorithm for the distribution of actors within the cluster.
class PartitionActivatorSelector
{
    private volatile ConcurrentDictionary<string, RendezvousFast> _hasherByKind = new();

    public void Update(Member[] members)
    {
        // Precreate RendezvousFast hasher instances by each Kind the cluster supports.
        Dictionary<string, RendezvousFast> newHasherByKind = members
            .SelectMany(member => member.Kinds.Select(kind => (member, kind)))
            .GroupBy(v => v.kind)
            .ToDictionary(
                v => v.Key,
                v => new RendezvousFast(v.Select(t => t.member)));

        // Assign at-once in an atomic manner rather than doing a Clear and re-assigning Keys
        // as that would allow _hasherByKind to be read in inconsistent states by GetOwne
        _hasherByKind = new(newHasherByKind);
    }

    public string GetOwnerAddress(ClusterIdentity key)
    {
        if (_hasherByKind.TryGetValue(key.Kind, out var hasher))
            return hasher.GetOwnerMemberByIdentity(key.Identity);
        return "";
    }
}