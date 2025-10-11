// -----------------------------------------------------------------------
// <copyright file="GossipState.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Proto.Logging;

namespace Proto.Cluster.Gossip;

internal static class GossipStateManagement
{
    private static readonly ILogger Logger = Log.CreateLogger("GossipStateManagement");

    private static GossipKeyValue EnsureEntryExists(GossipState.Types.GossipMemberState memberState, string key)
    {
        if (memberState.Values.TryGetValue(key, out var value))
        {
            return value;
        }

        value = new GossipKeyValue();
        memberState.Values.Add(key, value);

        return value;
    }

    public static GossipState.Types.GossipMemberState EnsureMemberStateExists(GossipState state, string memberId)
    {
        if (state.Members.TryGetValue(memberId, out var memberState))
        {
            return memberState;
        }

        //ensure the member state exists
        memberState = new GossipState.Types.GossipMemberState
        {
            Values =
            {
                {
                    //make sure we have a heartbeat entry
                    GossipKeys.Heartbeat, new GossipKeyValue
                    {
                        Value = Any.Pack(new MemberHeartbeat
                        {
                            ActorStatistics = new ActorStatistics()
                        }),
                        LocalTimestampUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    }
                }
            }
        };

        state.Members.Add(memberId, memberState);

        return memberState;
    }

    // Merge two states and return the new state and associated updates without touching the inputs
    public static (GossipState newState, IReadOnlyCollection<GossipUpdate> updates, HashSet<string> updatedKeys) MergeStates(
        GossipState localState,
        GossipState remoteState
    )
    {
        var newState = localState.Clone();
        var updates = new List<GossipUpdate>();
        var updatedKeys = new HashSet<string>();

        foreach (var (memberId, remoteMemberState) in remoteState.Members)
        {
            if (!newState.Members.TryGetValue(memberId, out var newMemberState))
            {
                var clonedMemberState = remoteMemberState.Clone();

                foreach (var (key, value) in clonedMemberState.Values)
                {
                    value.LocalTimestampUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    updates.Add(new GossipUpdate(memberId, key, value.Value, value.SequenceNumber));
                    updatedKeys.Add(key);
                }

                newState.Members.Add(memberId, clonedMemberState);
                continue;
            }

            foreach (var (key, remoteValue) in remoteMemberState.Values)
            {
                if (!newMemberState.Values.TryGetValue(key, out var existingValue))
                {
                    var newValue = remoteValue.Clone();
                    newValue.LocalTimestampUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    newMemberState.Values.Add(key, newValue);
                    updates.Add(new GossipUpdate(memberId, key, newValue.Value, newValue.SequenceNumber));
                    updatedKeys.Add(key);
                    continue;
                }

                if (remoteValue.SequenceNumber <= existingValue.SequenceNumber)
                {
                    continue;
                }

                var replacedValue = remoteValue.Clone();
                replacedValue.LocalTimestampUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                newMemberState.Values[key] = replacedValue;
                updates.Add(new GossipUpdate(memberId, key, replacedValue.Value, replacedValue.SequenceNumber));
                updatedKeys.Add(key);
            }
        }

        return (newState, updates, updatedKeys);
    }

    public static long SetKey(GossipState state, string key, IMessage value, string memberId, long sequenceNo)
    {
        //if entry does not exist, add it
        var memberState = EnsureMemberStateExists(state, memberId);
        var entry = EnsureEntryExists(memberState, key);
        entry.LocalTimestampUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        sequenceNo++;

        entry.SequenceNumber = sequenceNo;
        entry.Value = Any.Pack(value);

        return sequenceNo;
    }

    public static (bool Consensus, T value) CheckConsensus<T>(
        IContext ctx,
        GossipState state,
        string myId,
        ImmutableHashSet<string> members,
        string valueKey
    ) where T : IMessage, new() =>
        CheckConsensus<T, T>(ctx, state, myId, members, valueKey, v => v);

    public static (bool Consensus, TV value) CheckConsensus<T, TV>(
        IContext? ctx,
        GossipState state,
        string myId,
        ImmutableHashSet<string> members,
        string valueKey,
        Func<T, TV> extractValue
    ) where T : IMessage, new()
    {
        var logger = ctx?.Logger()?.BeginMethodScope();

        if (state.Members.Count == 0)
        {
            logger?.LogDebug("No members found for consensus check");

            return (false, default!);
        }

        logger?.LogDebug("Checking consensus");

        if (!state.Members.TryGetValue(myId, out var ownMemberState))
        {
            logger?.LogDebug("I can't find myself");

            return (false, default!);
        }

        var ownValue = GetConsensusValue(ownMemberState);

        if (ownValue is null)
        {
            logger?.LogDebug("I don't have any value for {Key}", valueKey);

            return (false, default!);
        }

        foreach (var (memberId, memberState) in state.Members)
        {
            //skip blocked members
            if (!members.Contains(memberId))
            {
                logger?.LogDebug("Member is not part of cluster {MemberId}", memberId);

                continue;
            }

            var consensusValue = GetConsensusValue(memberState);

            if (consensusValue is null || !ownValue.Equals(consensusValue))
            {
                return (false, default!);
            }
        }

        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug("Reached Consensus {Key}:{Value} - {State}", valueKey, ownValue, state);
        }

        return (true, ownValue);

        TV? GetConsensusValue(GossipState.Types.GossipMemberState memberState)
        {
            var stateByKey = memberState.GetMemberStateByKey<T>(valueKey);

            return stateByKey is not null ? extractValue(stateByKey) : default;
        }
    }

    private static T? GetMemberStateByKey<T>(this GossipState.Types.GossipMemberState memberState, string key)
        where T : IMessage, new()
    {
        if (!memberState.Values.TryGetValue(key, out var entry))
        {
            return default;
        }

        var topology = entry.Value.Unpack<T>();

        return topology;
    }
}