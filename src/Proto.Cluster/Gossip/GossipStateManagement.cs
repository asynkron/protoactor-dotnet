// -----------------------------------------------------------------------
// <copyright file="GossipState.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Proto.Logging;

namespace Proto.Cluster.Gossip
{
    internal static class GossipStateManagement
    {
        private static readonly ILogger Logger = Log.CreateLogger("GossipStateManagement");

        private static GossipKeyValue EnsureEntryExists(GossipState.Types.GossipMemberState memberState, string key)
        {
            if (memberState.Values.TryGetValue(key, out var value)) return value;

            value = new GossipKeyValue();
            memberState.Values.Add(key, value);

            return value;
        }

        public static GossipState.Types.GossipMemberState EnsureMemberStateExists(GossipState state, string memberId)
        {
            if (state.Members.TryGetValue(memberId, out var memberState)) return memberState;

            memberState = new GossipState.Types.GossipMemberState();
            state.Members.Add(memberId, memberState);

            return memberState;
        }

        public static IReadOnlyCollection<GossipUpdate> MergeState(GossipState localState, GossipState remoteState, out GossipState newState, out HashSet<string> updatedKeys)
        {
            newState = localState.Clone();
            var updates = new List<GossipUpdate>();
            updatedKeys = new HashSet<string>();

            foreach (var (memberId, remoteMemberState) in remoteState.Members)
            {
                //this entry does not exist in newState, just copy all of it
                if (!newState.Members.ContainsKey(memberId))
                {
                    newState.Members.Add(memberId, remoteMemberState);

                    foreach (var entry in remoteMemberState.Values)
                    {
                        updates.Add(new GossipUpdate(memberId, entry.Key, entry.Value.Value, entry.Value.SequenceNumber));
                        entry.Value.LocalTimestampUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        updatedKeys.Add(entry.Key);
                    }
                    continue;
                }

                //this entry exists in both newState and remoteState, we should merge them
                var newMemberState = newState.Members[memberId];

                foreach (var (key, remoteValue) in remoteMemberState.Values)
                {
                    //this entry does not exist in newMemberState, just copy all of it
                    if (!newMemberState.Values.ContainsKey(key))
                    {
                        newMemberState.Values.Add(key, remoteValue);
                        updates.Add(new GossipUpdate(memberId, key, remoteValue.Value, remoteValue.SequenceNumber));
                        remoteValue.LocalTimestampUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        updatedKeys.Add(key);
                        continue;
                    }

                    var newValue = newMemberState.Values[key];

                    //remote value is older, ignore
                    if (remoteValue.SequenceNumber <= newValue.SequenceNumber) continue;

                    //just replace the existing value
                    newMemberState.Values[key] = remoteValue;
                    updates.Add(new GossipUpdate(memberId, key, remoteValue.Value, remoteValue.SequenceNumber));
                    remoteValue.LocalTimestampUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    updatedKeys.Add(key);
                }
            }

            return updates;
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
        ) where T : IMessage, new() => CheckConsensus<T, T>(ctx, state, myId, members, valueKey, v => v);

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

            try
            {
                if (state.Members.Count == 0)
                {
                    logger?.LogDebug("No members found for consensus check");
                    return (false, default);
                }

                logger?.LogDebug("Checking consensus");

                if (!state.Members.TryGetValue(myId, out var ownMemberState))
                {
                    logger?.LogDebug("I can't find myself");
                    return (false, default);
                }

                var ownValue = GetConsensusValue(ownMemberState);
                if (ownValue is null)
                {
                    logger?.LogDebug("I don't have any value for {Key}", valueKey);
                    return (false, default);
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
                        return (false, default);
                    }
                }
                Logger.LogDebug("Reached Consensus {Key}:{Value} - {State}", valueKey,ownValue, state);
                return (true, ownValue);
            }
            catch (Exception x)
            {
                logger?.LogError(x, "Check Consensus failed");
                Logger.LogError(x, "Check Consensus failed");
                return (false, default);
            }

            TV? GetConsensusValue(GossipState.Types.GossipMemberState memberState)
            {
                var stateByKey = memberState.GetMemberStateByKey<T>(valueKey);

                return stateByKey is not null ? extractValue(stateByKey) : default;
            }
        }

        private static T? GetMemberStateByKey<T>(this GossipState.Types.GossipMemberState memberState, string key) where T : IMessage, new()
        {
            if (!memberState.Values.TryGetValue(key, out var entry))
                return default;

            var topology = entry.Value.Unpack<T>();
            return topology;
        }
    }
}