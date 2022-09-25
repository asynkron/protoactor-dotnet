// -----------------------------------------------------------------------
// <copyright file="PartitionActivatorSelector.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;
using System.Linq;

namespace Proto.Cluster.PartitionActivator;

//this class is responsible for translating between Identity->activator member
//this is the key algorithm for the distribution of actors within the cluster.
internal class PartitionActivatorSelector
{
    private volatile ImmutableDictionary<string, RendezvousFast> _hasherByKind =
        ImmutableDictionary<string, RendezvousFast>.Empty;

    public void Update(Member[] members)
    {
        // Precreate RendezvousFast hasher instances by each Kind the cluster supports.
        var newHasherByKind = members
            .SelectMany(member => member.Kinds.Select(kind => (member, kind)))
            .GroupBy(v => v.kind)
            .ToImmutableDictionary(
                v => v.Key,
                v => new RendezvousFast(v.Select(t => t.member)));

        // Assign at-once in an atomic manner, so that GetOwnerAddress is always thread-safe.
        _hasherByKind = newHasherByKind;
    }

    public string GetOwnerAddress(ClusterIdentity key)
    {
        if (_hasherByKind.TryGetValue(key.Kind, out var hasher))
        {
            return hasher.GetOwnerMemberByIdentity(key.Identity);
        }

        return "";
    }
}