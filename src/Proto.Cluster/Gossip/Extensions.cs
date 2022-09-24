// -----------------------------------------------------------------------
// <copyright file="Extensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Proto.Cluster.Gossip;

public static class Extensions
{
    internal static (bool, T?) HasConsensus<T>(this IEnumerable<T?> enumerable)
    {
        using var enumerator = enumerable.GetEnumerator();

        if (!enumerator.MoveNext() || enumerator.Current is null)
        {
            return default;
        }

        var first = enumerator.Current;

        while (enumerator.MoveNext())
        {
            if (enumerator.Current?.Equals(first) != true)
            {
                return default;
            }
        }

        return (true, first);
    }

    internal static (IConsensusHandle<T> handle, ConsensusCheck check) Build<T>(
        this IConsensusCheckDefinition<T> consensusDefinition,
        Action cancel
    )
        where T : notnull
    {
        var handle = new GossipConsensusHandle<T>(cancel);

        var check = CreateConsensusCheck(
            consensusDefinition,
            consensusValue => handle.TrySetConsensus(consensusValue),
            () => handle.TryResetConsensus()
        );

        return (handle, check);
    }

    private static ConsensusCheck CreateConsensusCheck<T>(
        this IConsensusCheckDefinition<T> consensusDefinition,
        Action<T> onConsensus,
        Action lostConsensus
    ) where T : notnull
    {
        var hasConsensus = consensusDefinition.Check;
        // Close over previous state, only callback on change
        var hadConsensus = false;

        void CheckConsensus(GossipState state, IImmutableSet<string> members)
        {
            var (consensus, value) = hasConsensus(state, members);

            if (consensus)
            {
                if (hadConsensus)
                {
                    return;
                }

                onConsensus(value);
                hadConsensus = true;
            }
            else if (hadConsensus)
            {
                lostConsensus();
                hadConsensus = false;
            }
        }

        return new ConsensusCheck(CheckConsensus, consensusDefinition.AffectedKeys);
    }
}