// -----------------------------------------------------------------------
// <copyright file="MemberStrategyManager.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Proto.Logging;

namespace Proto.Cluster;

/// <summary>
///     Maintains <see cref="IMemberStrategy" /> instances for each cluster kind and provides activator lookup.
///     Extracted from <see cref="MemberList" /> to keep membership concerns separated from activation strategy logic.
/// </summary>
internal class MemberStrategyManager
{
    private static readonly ILogger Logger = Log.CreateLogger<MemberStrategyManager>();
    private readonly Cluster _cluster;
    private ImmutableDictionary<string, IMemberStrategy> _memberStrategyByKind =
        ImmutableDictionary<string, IMemberStrategy>.Empty;

    internal MemberStrategyManager(Cluster cluster) => _cluster = cluster;

    internal Member? GetActivator(string kind, string requestSourceAddress)
    {
        if (_memberStrategyByKind.TryGetValue(kind, out var memberStrategy))
        {
            return memberStrategy.GetActivator(requestSourceAddress);
        }

        Logger.DidNotFindActivatorForKind(kind);

        return null;
    }

    internal void AddMember(Member newMember)
    {
        foreach (var kind in newMember.Kinds)
        {
            if (!_memberStrategyByKind.ContainsKey(kind))
            {
                _memberStrategyByKind = _memberStrategyByKind.SetItem(kind, GetMemberStrategyByKind(kind));
            }

            _memberStrategyByKind[kind].AddMember(newMember);
        }
    }

    internal void RemoveMember(Member member)
    {
        foreach (var k in member.Kinds)
        {
            if (!_memberStrategyByKind.TryGetValue(k, out var ms))
            {
                continue;
            }

            ms.RemoveMember(member);

            if (ms.GetAllMembers().Count == 0)
            {
                _memberStrategyByKind = _memberStrategyByKind.Remove(k);
            }
        }
    }

    private IMemberStrategy GetMemberStrategyByKind(string kind)
    {
        var clusterKind = _cluster.TryGetClusterKind(kind);

        if (clusterKind?.Strategy != null)
        {
            return clusterKind.Strategy;
        }

        return _cluster.Config.MemberStrategyBuilder(_cluster, kind) ?? new SimpleMemberStrategy();
    }
}

