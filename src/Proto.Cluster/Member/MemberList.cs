// -----------------------------------------------------------------------
// <copyright file="MemberList.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Proto.Cluster.Data;
using Proto.Remote;
using Proto.Utils;

namespace Proto.Cluster
{
    //This class is responsible for figuring out what members are currently active in the cluster
    //it will receive a list of Members from the IClusterProvider
    //from that, we calculate a delta, which members joined, or left.

    [PublicAPI]
    public record MemberList
    {
        private readonly Cluster _cluster;
        private readonly EventStream _eventStream;
        private static readonly ILogger Logger = Log.CreateLogger<MemberList>();
        private uint _currentMembershipHashCode = uint.MinValue;

        private readonly IRootContext _root;
        private readonly ActorSystem _system;
        private ImmutableDictionary<string, int> _indexByAddress = ImmutableDictionary<string, int>.Empty;
        private ImmutableDictionary<string, ClusterTopologyNotification> _memberState = ImmutableDictionary<string, ClusterTopologyNotification>.Empty;
        private TaskCompletionSource<bool> _topologyConsensus = new ();

        private Member? _leader;

        //TODO: the members here are only from the cluster provider
        //The partition lookup broadcasts and use broadcasted information
        //meaning the partition infra might be ahead of this list.
        //come up with a good solution to keep all this in sync
        private ImmutableDictionary<string, Member> _members = ImmutableDictionary<string, Member>.Empty;
        private ImmutableDictionary<int, Member> _membersByIndex = ImmutableDictionary<int, Member>.Empty;

        private ImmutableDictionary<string, IMemberStrategy> _memberStrategyByKind = ImmutableDictionary<string, IMemberStrategy>.Empty;
        private readonly ConcurrentSet<string> _bannedMembers = new();
        private int _nextMemberIndex;

        public MemberList(Cluster cluster)
        {
            _cluster = cluster;
            _system = _cluster.System;
            _root = _system.Root;
            _eventStream = _system.EventStream;
            _cluster.System.EventStream.Subscribe<ClusterTopologyNotification>(OnClusterTopologyNotification);
        }

        public Task TopologyConsensus() => _topologyConsensus.Task;

        private void OnClusterTopologyNotification(ClusterTopologyNotification ctn)
        {
            lock (this)
            {
                _memberState = _memberState.SetItem(ctn.MemberId, ctn);
                var excludeBannedMembers = _memberState.Keys.Where(k => _bannedMembers.Contains(k));
                _memberState = _memberState.RemoveRange(excludeBannedMembers);
                
                var everyoneInAgreement = _memberState.Values.All(x => x.MembershipHashCode == _currentMembershipHashCode);

                if (everyoneInAgreement && !_topologyConsensus.Task.IsCompleted)
                {
                    //anyone awaiting this instance will now proceed
                    Logger.LogInformation("[MemberList] Topology consensus");
                    _topologyConsensus.TrySetResult(true);
                    var leaderId = LeaderElection.Elect(_memberState);
                    var newLeader = _members[leaderId];
                    if (!newLeader.Equals(_leader))
                    {
                        _leader = newLeader;
                        _system.EventStream.Publish(new LeaderElected(newLeader));

                        // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                        if (_leader.Id == _system.Id)
                        {
                            Logger.LogInformation("[MemberList] I am leader {Id}", _leader.Id);
                        }
                        else
                        {
                            Logger.LogInformation("[MemberList] Member {Id} is leader", _leader.Id);
                        }
                    }
                }
                else if (!everyoneInAgreement && _topologyConsensus.Task.IsCompleted)
                {
                    //we toggled from consensus to not consensus.
                    //create a new completion source for new awaiters to await
                    _topologyConsensus = new TaskCompletionSource<bool>();
                }
                
                

                Logger.LogDebug("[MemberList] Got ClusterTopologyNotification {ClusterTopologyNotification}, Consensus {Consensus}, Members {Members}", ctn, everyoneInAgreement,_memberState.Count);
            }
        }

        public Member? GetActivator(string kind, string requestSourceAddress)
        {
            lock (this)
            {
                if (_memberStrategyByKind.TryGetValue(kind, out var memberStrategy))
                    return memberStrategy.GetActivator(requestSourceAddress);

                Logger.LogError("[MemberList] MemberList did not find any activator for kind '{Kind}'", kind);
                return null;
            }
        }

        public void UpdateClusterTopology(IReadOnlyCollection<Member> statuses)
        {
            lock (this)
            {
                Logger.LogDebug("[MemberList] Updating Cluster Topology");
                var topology = new ClusterTopology {EventId = Member.GetMembershipHashCode(statuses)};

                //TLDR:
                //this method basically filters out any member status in the banned list
                //then makes a delta between new and old members
                //notifying the cluster accordingly which members left or joined

                //these are all members that are currently active
                var nonBannedStatuses =
                    statuses
                        .Where(s => !_bannedMembers.Contains(s.Id))
                        .ToArray();

                var newMembershipHashCode = Member.GetMembershipHashCode(nonBannedStatuses);

                //same topology, bail out
                if (newMembershipHashCode == _currentMembershipHashCode)
                {
                    return;
                }

                _currentMembershipHashCode = newMembershipHashCode;

                //these are the member IDs hashset of currently active members
                var newMemberIds =
                    nonBannedStatuses
                        .Select(s => s.Id)
                        .ToImmutableHashSet();

                //these are all members that existed before, but are not in the current nonBannedMemberStatuses
                var membersThatLeft =
                    _members
                        .Where(m => !newMemberIds.Contains(m.Key))
                        .Select(m => m.Value)
                        .ToArray();

                //notify that these members left
                foreach (var memberThatLeft in membersThatLeft)
                {
                    MemberLeave(memberThatLeft);
                    topology.Left.Add(new Member
                        {
                            Host = memberThatLeft.Host,
                            Port = memberThatLeft.Port,
                            Id = memberThatLeft.Id,
                            Index = memberThatLeft.Index
                        }
                    );
                }

                //these are all members that are new and did not exist before
                var membersThatJoined =
                    nonBannedStatuses
                        .Where(m => !_members.ContainsKey(m.Id))
                        .ToArray();

                //notify that these members joined
                foreach (var memberThatJoined in membersThatJoined)
                {
                    // Node local short identifier
                    MemberJoin(memberThatJoined);
                    topology.Joined.Add(new Member
                        {
                            Host = memberThatJoined.Host,
                            Port = memberThatJoined.Port,
                            Id = memberThatJoined.Id,
                            Index = memberThatJoined.Index
                        }
                    );
                }

                topology.Members.AddRange(_members.Values);

                Logger.LogDebug("[MemberList] Published ClusterTopology event {ClusterTopology}", topology);

                if (topology.Joined.Count > 0) Logger.LogInformation("[MemberList] Cluster members joined {MembersJoined}", topology.Joined);

                if (topology.Left.Count > 0) Logger.LogInformation("[MemberList] Cluster members left {MembersJoined}", topology.Left);

                _eventStream.Publish(topology);

                foreach (var m in topology.Members)
                {
                    //add any missing member to the hashcode dict
                    if (!_memberState.ContainsKey(m.Id))
                    {
                        _memberState = _memberState.Add(m.Id,new ClusterTopologyNotification()
                        {
                            MemberId = m.Id
                        });
                    }
                }

                //Notify other members...
                BroadcastEvent(new ClusterTopologyNotification
                    {
                        MemberId = _cluster.System.Id,
                        MembershipHashCode = _currentMembershipHashCode,
                        LeaderId = _leader == null? "": _leader.Id,
                    }, true
                );
            }


            void MemberLeave(Member memberThatLeft)
            {
                //update MemberStrategy
                foreach (var k in memberThatLeft.Kinds)
                {
                    if (!_memberStrategyByKind.TryGetValue(k, out var ms)) continue;

                    ms.RemoveMember(memberThatLeft);

                    if (ms.GetAllMembers().Count == 0)
                    {
                        _memberStrategyByKind = _memberStrategyByKind.Remove(k);
                    }
                }

                _bannedMembers.Add(memberThatLeft.Id);

                _members = _members.Remove(memberThatLeft.Id);
                _membersByIndex = _membersByIndex.Remove(memberThatLeft.Index);

                if (_indexByAddress.TryGetValue(memberThatLeft.Address, out _))
                    _indexByAddress = _indexByAddress.Remove(memberThatLeft.Address);

                var endpointTerminated = new EndpointTerminatedEvent {Address = memberThatLeft.Address};
                Logger.LogDebug("[MemberList] Published event {@EndpointTerminated}", endpointTerminated);
                _cluster.System.EventStream.Publish(endpointTerminated);
            }

            void MemberJoin(Member newMember)
            {
                newMember.Index = _nextMemberIndex++;
                
                _members = _members.Add(newMember.Id, newMember);
                _membersByIndex = _membersByIndex.Add(newMember.Index, newMember);
                _indexByAddress = _indexByAddress.Add(newMember.Address, newMember.Index);

                foreach (var kind in newMember.Kinds)
                {
                    if (!_memberStrategyByKind.ContainsKey(kind))
                    {
                        _memberStrategyByKind = _memberStrategyByKind.SetItem(kind, GetMemberStrategyByKind(kind));
                    }

                    _memberStrategyByKind[kind].AddMember(newMember);
                }
            }
        }

        //
        private IMemberStrategy GetMemberStrategyByKind(string kind)
        {
            //Try get the cluster kind
            var clusterKind = _cluster.TryGetClusterKind(kind);

            //if it exists, and if it has a strategy
            if (clusterKind?.Strategy != null)
            {
                //use that strategy
                return clusterKind.Strategy;
            }
            
            //otherwise, use whatever member strategy the default builder says
            return _cluster.Config!.MemberStrategyBuilder(_cluster, kind) ?? new SimpleMemberStrategy();
        }

        /// <summary>
        ///     broadcast a message to all members eventstream
        /// </summary>
        /// <param name="message"></param>
        /// <param name="includeSelf"></param>
        public void BroadcastEvent(object message, bool includeSelf = true)
        {
            foreach (var (id, member) in _members)
            {
                if (!includeSelf && id == _cluster.System.Id) continue;

                var pid = PID.FromAddress(member.Address, "eventstream");

                try
                {
                    _system.Root.Send(pid, message);
                }
                catch (Exception)
                {
                    Logger.LogError("[MemberList] Failed to broadcast {Message} to {Pid}", message, pid);
                }
            }
        }

        public bool ContainsMemberId(string memberId) => _members.ContainsKey(memberId);

        public bool TryGetMember(string memberId, out Member? value) => _members.TryGetValue(memberId, out value);

        public bool TryGetMemberIndexByAddress(string address, out int value) => _indexByAddress.TryGetValue(address, out value);

        public bool TryGetMemberByIndex(int memberIndex, out Member? value) => _membersByIndex.TryGetValue(memberIndex, out value);

        public Member[] GetAllMembers() => _members.Values.ToArray();

        public void DumpState()
        {
            foreach (var m in _members)
            {
                Console.WriteLine(m);
            }
        }
    }
}