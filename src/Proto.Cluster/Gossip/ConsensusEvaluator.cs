// -----------------------------------------------------------------------
// <copyright file="ConsensusEvaluator.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Proto.Cluster.Gossip;

internal static class ConsensusEvaluator
{
    /// <summary>
    ///     Evaluates whether all selected gossip values are equal for the provided members.
    /// </summary>
    /// <typeparam name="T">Type of the value to compare.</typeparam>
    /// <param name="state">Current gossip state.</param>
    /// <param name="memberIds">Members that must participate in the consensus.</param>
    /// <param name="mappers">Value selectors that extract a comparable value from each member state.</param>
    /// <returns>
    ///     Tuple indicating if consensus was reached, the agreed value when available and the mapped
    ///     value tuples used during the evaluation.
    /// </returns>
    internal static (bool consensus, T? value, (string member, string key, T value)[] tuples) HasConsensus<T>(
        GossipState state,
        IImmutableSet<string> memberIds,
        IEnumerable<Func<KeyValuePair<string, GossipState.Types.GossipMemberState>, (string member, string key, T value)>> mappers
    ) where T : notnull
    {
        var memberStates = state.Members
            .Where(kv => memberIds.Contains(kv.Key))
            .Select(kv => kv)
            .ToArray();

        if (memberStates.Length < memberIds.Count)
        {
            return (false, default, Array.Empty<(string member, string key, T value)>());
        }

        var valueTuples = memberStates
            .SelectMany(memberState => mappers.Select(mapper => mapper(memberState)))
            .ToArray();

        var (hasConsensus, value) = valueTuples.Select(it => it.value).HasConsensus();

        return (hasConsensus, value, valueTuples);
    }
}

