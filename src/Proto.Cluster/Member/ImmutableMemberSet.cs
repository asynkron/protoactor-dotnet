// -----------------------------------------------------------------------
// <copyright file="ImmutableMemberSet.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Proto.Cluster;

public sealed class ImmutableMemberSet
{
    public static readonly ImmutableMemberSet Empty = new(Array.Empty<Member>());

    public ImmutableMemberSet(Member[] members)
    {
        Members = members.OrderBy(m => m.Id).ToArray();
        TopologyHash = Member.TopologyHash(members);
        Lookup = members.ToImmutableDictionary(m => m.Id);
    }

    public uint TopologyHash { get; }
    public IReadOnlyCollection<Member> Members { get; }
    public ImmutableDictionary<string, Member> Lookup { get; }

    private bool Equals(ImmutableMemberSet other) => TopologyHash == other.TopologyHash;

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != GetType())
        {
            return false;
        }

        return Equals((ImmutableMemberSet)obj);
    }

    public override int GetHashCode() => (int)TopologyHash;

    public bool Contains(string id) => Lookup.ContainsKey(id);

    public ImmutableMemberSet Except(ImmutableMemberSet other)
    {
        var both = Members.Except(other.Members).ToArray();

        return new ImmutableMemberSet(both);
    }

    public ImmutableMemberSet Except(IEnumerable<string> other)
    {
        var otherSet = other.ToImmutableHashSet();
        var both = Members.Where(m => !otherSet.Contains(m.Id)).ToArray();

        return new ImmutableMemberSet(both);
    }

    public ImmutableMemberSet Union(ImmutableMemberSet other)
    {
        var both = Members.Union(other.Members).ToArray();

        return new ImmutableMemberSet(both);
    }

    public Member? GetById(string id)
    {
        Lookup.TryGetValue(id, out var res);

        return res;
    }
}