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

namespace Proto.Cluster.Gossip
{
    public static class GossipStateManagement
    {
        public static GossipKeyValue EnsureEntryExists(GossipMemberState memberState, string key)
        {
            if (!memberState.Values.TryGetValue(key,out var value))
            {
                value = new GossipKeyValue();
                memberState.Values.Add(key, value);
            }

            return value;
        }
        
        public static GossipMemberState EnsureMemberStateExists(GossipState state, string memberId)
        {
            if (!state.Members.TryGetValue(memberId, out var memberState))
            {
                memberState = new GossipMemberState();
                state.Members.Add(memberId, memberState);
            }

            return memberState;
        }

        public static bool MergeState(GossipState state, GossipState remoteState, out  GossipState newState)
        {
            newState = state.Clone();
            var dirty = false;

            foreach (var (memberId, remoteMemberState) in remoteState.Members)
            {
                //this entry does not exist in newState, just copy all of it
                if (!newState.Members.ContainsKey(memberId))
                {
                    newState.Members.Add(memberId, remoteMemberState);
                    dirty = true;
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
                        dirty = true;
                        continue;
                    }

                    var newValue = newMemberState.Values[key];

                    if (remoteValue.SequenceNumber > newValue.SequenceNumber)
                    {
                        dirty = true;
                        //just replace the existing value
                        newMemberState.Values[key] = remoteValue;
                    }
                }
            }

            return dirty;
        }

        public static void SetKey(GossipState state, string key, IMessage value, string memberId, ref long sequenceNo)
        {
            //if entry does not exist, add it
            var memberState = GossipStateManagement.EnsureMemberStateExists(state, memberId);
            var entry = GossipStateManagement.EnsureEntryExists(memberState, key);

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
                var newMemberState = new GossipMemberState();
                

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

            return (pendingOffsets, newState);
        }
        
        public static (bool, uint) ElectLeader(GossipState state, string myId, ImmutableHashSet<string> members)
        {
            try
            {
                var hashes = new List<(string,uint)>();
                
                foreach (var (memberId, memberState) in state.Members)
                {
                    //skip banned members
                    if (!members.Contains(memberId))
                        continue;
                    
                    var topology = memberState.GetTopology();

                    if (topology == null)
                    {
                        //Console.WriteLine(myId+ " null topology" + memberId);
                        hashes.Add((memberId,0));
                        continue;
                    }
                    hashes.Add((memberId,topology.MembershipHashCode));
                }

                var first = hashes.FirstOrDefault();

                if (hashes.All(h => h.Item2 == first.Item2))
                {
                    //all members have the same hash
                    return (true, first.Item2);
                }

                return (false, 0);
            }
            catch (Exception x)
            {
                Console.WriteLine(x);
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