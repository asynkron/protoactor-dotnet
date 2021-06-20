// -----------------------------------------------------------------------
// <copyright file="GossipState.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
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
    public static class GossipStateManagement
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

        public static IReadOnlyCollection<GossipUpdate> MergeState(GossipState localState, GossipState remoteState, out  GossipState mergedState)
        {
            mergedState = localState.Clone();
            var updates = new List<GossipUpdate>();

            foreach (var (memberId, remoteMemberState) in remoteState.Members)
            {
                //this entry does not exist in newState, just copy all of it
                if (!mergedState.Members.ContainsKey(memberId))
                {
                    mergedState.Members.Add(memberId, remoteMemberState);

                    foreach (var entry in remoteMemberState.Values)
                    {
                        updates.Add(new GossipUpdate(memberId,entry.Key,entry.Value.Value,entry.Value.SequenceNumber));
                    }
                    continue;
                }

                //this entry exists in both newState and remoteState, we should merge them
                var newMemberState = mergedState.Members[memberId];

                foreach (var (key, remoteValue) in remoteMemberState.Values)
                {
                    //this entry does not exist in newMemberState, just copy all of it
                    if (!newMemberState.Values.ContainsKey(key))
                    {
                        newMemberState.Values.Add(key, remoteValue);
                        updates.Add(new GossipUpdate(memberId,key,remoteValue.Value,remoteValue.SequenceNumber));
                        continue;
                    }

                    var newValue = newMemberState.Values[key];

                    //remote value is older, ignore
                    if (remoteValue.SequenceNumber <= newValue.SequenceNumber) continue;
                    
                    //just replace the existing value
                    newMemberState.Values[key] = remoteValue;
                    updates.Add(new GossipUpdate(memberId,key,remoteValue.Value,remoteValue.SequenceNumber));
                }
            }

            return updates;
        }

        public static void SetKey(GossipState state, string key, IMessage value, string memberId, ref long sequenceNo)
        {
            //if entry does not exist, add it
            var memberState = EnsureMemberStateExists(state, memberId);
            var entry = EnsureEntryExists(memberState, key);

            sequenceNo++;

            entry.SequenceNumber = sequenceNo;
            entry.Value = Any.Pack(value);
        }

        public static (ImmutableDictionary<string, long> pendingOffsets, GossipState state) FilterGossipStateForMember(GossipState state, ImmutableDictionary<string, long> offsets, string targetMemberId)
        {
            var newState = new GossipState();

            var pendingOffsets = offsets;
            //for each member
            foreach (var (memberId, memberState) in state.Members)
            {
                //we dont need to send back state to the owner of the state
                if (memberId == targetMemberId)
                {
                    continue;
                }
                
                //create an empty state
                var newMemberState = new GossipState.Types.GossipMemberState();

                var watermarkKey = $"{targetMemberId}.{memberId}";
                //get the watermark 
                offsets.TryGetValue(watermarkKey, out long watermark);
                var newWatermark = watermark;

                //for each value in member state
                foreach (var (key, value) in memberState.Values)
                {
                    if (value.SequenceNumber <= watermark)
                        continue;
                    
                    if (value.SequenceNumber > newWatermark)
                        newWatermark = value.SequenceNumber;
                    
                    newMemberState.Values.Add(key, value);
                }

                //don't send memberStates that we have no new data for 
                if (newMemberState.Values.Count > 0)
                {
                    newState.Members.Add(memberId, newMemberState);
                }

                pendingOffsets = pendingOffsets.SetItem(watermarkKey, newWatermark);
            }

            //make sure to clone to make it a separate copy, avoid race conditions on mutate
            return (pendingOffsets, newState.Clone());
        }
        
        public static (bool Consensus, ulong TopologyHash) CheckConsensus(IContext ctx,  GossipState state, string myId, ImmutableHashSet<string> members)
        {
            var logger = ctx.Logger()?.BeginMethodScope();
            try
            {
                var hashes = new List<(string MemberId,ulong TopologyHash)>();

                if (state.Members.Count == 0)
                {
                    logger?.LogDebug("No members found for consensus check");
                }
                
                logger?.LogDebug("Checking consensus");
                foreach (var (memberId, memberState) in state.Members)
                {
                    //skip banned members
                    if (!members.Contains(memberId))
                    {
                        logger?.LogDebug("Member is not part of cluster {MemberId}", memberId);
                        continue;
                    }

                    var topology = memberState.GetTopology();

                    //member does not yet have a topology, default to empty
                    if (topology == null)
                    {
                        if (memberId == myId)
                        {
                            logger?.LogDebug("I can't find myself");
                        }
                        else
                        {
                            logger?.LogDebug("Remote: {OtherMemberId} has no topology using TopologyHash 0", memberId);    
                        }
                        
                        
                        hashes.Add((memberId,0));
                        continue;
                    }
                    
                    hashes.Add((memberId,topology.TopologyHash));
                    logger?.LogDebug("Remote: {OtherMemberId} - {OtherTopologyHash} - {OtherMemberCount}", memberId, topology.TopologyHash, topology.Members.Count);
                }

                var first = hashes.FirstOrDefault();
                
                if (hashes.All(h => h.TopologyHash == first.TopologyHash) && first.TopologyHash != 0)
                {
                    Logger.LogDebug("Reached Consensus {TopologyHash} - {State}", first.TopologyHash, state);
                    logger?.LogDebug("Reached Consensus {TopologyHash} - {State}", first.TopologyHash, state);
                    //all members have the same hash
                    return (true, first.TopologyHash);
                }

                Logger.LogDebug("No Consensus {Hashes}, {State}", hashes.Select(h => h.TopologyHash),  state);
                logger?.LogDebug("No Consensus {Hashes}, {State}", hashes.Select(h => h.TopologyHash),  state);
                return (false, 0);
            }
            catch (Exception x)
            {
                logger?.LogError(x, "Check Consensus failed");
                Logger.LogError(x, "Check Consensus failed");
                return (false, 0);
            }

            // hashes.Add(topology.GetMembershipHashCode());
            //
            //     _memberState = _memberState.SetItem(ctn.MemberId, ctn);
            //     var excludeBannedMembers = _memberState.Keys.Where(k => _bannedMembers.Contains(k));
            //     _memberState = _memberState.RemoveRange(excludeBannedMembers);
            //     
            //     var everyoneInAgreement = _memberState.Values.All(x => x.MembershipHashCode == _currentMembershipHashCode);
            //
            //     if (everyoneInAgreement && !_topologyConsensus.Task.IsCompleted)
            //     {
            //         //anyone awaiting this instance will now proceed
            //         Logger.LogInformation("[MemberList] Topology consensus");
            //         _topologyConsensus.TrySetResult(true);
            //         var leaderId = LeaderElection.Elect(_memberState);
            //         var newLeader = _members[leaderId];
            //         if (!newLeader.Equals(_leader))
            //         {
            //             _leader = newLeader;
            //             _system.EventStream.Publish(new LeaderElected(newLeader));
            //
            //             // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
            //             if (_leader.Id == _system.Id)
            //             {
            //                 Logger.LogInformation("[MemberList] I am leader {Id}", _leader.Id);
            //             }
            //             else
            //             {
            //                 Logger.LogInformation("[MemberList] Member {Id} is leader", _leader.Id);
            //             }
            //         }
            //     }
            //     else if (!everyoneInAgreement && _topologyConsensus.Task.IsCompleted)
            //     {
            //         //we toggled from consensus to not consensus.
            //         //create a new completion source for new awaiters to await
            //         _topologyConsensus = new TaskCompletionSource<bool>();
            //     }
            //     
            //     
            //
            //     Logger.LogDebug("[MemberList] Got ClusterTopologyNotification {ClusterTopologyNotification}, Consensus {Consensus}, Members {Members}", ctn, everyoneInAgreement,_memberState.Count);
            // }
        }
    }
}