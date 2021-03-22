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
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Proto.Cluster.Data;
using Proto.Cluster.Events;
using Proto.Remote;
using Proto.Utils;

namespace Proto.Cluster
{
    //This class is responsible for figuring out what members are currently active in the cluster
    //it will receive a list of memberstatuses from the IClusterProvider
    //from that, we calculate a delta, which members joined, or left.

    //TODO: check usage and threadsafety.
    [PublicAPI]
    public record MemberList
    {
        private readonly Cluster _cluster;
        private readonly EventStream _eventStream;
        private readonly ILogger _logger;
        private uint _currentMembershipHashCode = uint.MinValue;

        private readonly IRootContext _root;

        private readonly ReaderWriterLockSlim _rwLock = new();
        private readonly ActorSystem _system;
        private ImmutableDictionary<string, int> _indexByAddress = ImmutableDictionary<string, int>.Empty;

        private LeaderInfo? _leader;

        //TODO: the members here are only from the cluster provider
        //The partition lookup broadcasts and use broadcasted information
        //meaning the partition infra might be ahead of this list.
        //come up with a good solution to keep all this in sync
        private ImmutableDictionary<string, Member> _members = ImmutableDictionary<string, Member>.Empty;
        private ImmutableDictionary<int, Member> _membersByIndex = ImmutableDictionary<int, Member>.Empty;

        private ImmutableDictionary<string, IMemberStrategy> _memberStrategyByKind = ImmutableDictionary<string, IMemberStrategy>.Empty;

        private int _nextMemberIndex;

        public MemberList(Cluster cluster)
        {
            _cluster = cluster;
            _system = _cluster.System;
            _root = _system.Root;
            _eventStream = _system.EventStream;

            _logger = Log.CreateLogger($"MemberList-{_cluster.LoggerId}");

            _bannedMembers = new ConcurrentSet<string>();
        }

        //TODO: actually use this to prevent banned members from rejoining
        private ConcurrentSet<string> _bannedMembers { get; init; }

        public bool IsLeader => _cluster.System.Id.Equals(_leader?.MemberId);

        public Member? GetActivator(string kind, string requestSourceAddress)
        {
            //TODO: clean this lock logic up
            var locked = _rwLock.TryEnterReadLock(1000);

            while (!locked)
            {
                _logger.LogDebug("MemberList did not acquire reader lock within 1 seconds, retry");
                locked = _rwLock.TryEnterReadLock(1000);
            }

            try
            {
                if (_memberStrategyByKind.TryGetValue(kind, out var memberStrategy))
                    return memberStrategy.GetActivator(requestSourceAddress);
                else
                {
                    _logger.LogError("MemberList did not find any activator for kind '{Kind}'", kind);
                    return null;
                }
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        //if the new leader is different from the current leader
        //notify via the event stream
        //TODO: cluster state update really. not only leader
        public void UpdateLeader(LeaderInfo leader)
        {
            //TODO: could likely be done better
            if (leader?.BannedMembers is not null)
            {
                foreach (var b in leader.BannedMembers)
                {
                    _bannedMembers.Add(b);
                }
            }

            if (leader?.MemberId == _leader?.MemberId)
            {
                //leader is the same as before, ignore
                //this can happen eg. if you run blocking queries with consul
                //if nothing changes within the blocking time, you will still get a result,
                //we will still get this notification.
                //we use the memberlist to diff the data against current state
                return;
            }

            var oldLeader = _leader;

            _leader = leader;

            _logger.LogInformation("Leader updated {Leader}", leader?.MemberId);
            _eventStream.Publish(new LeaderElectedEvent(leader, oldLeader));

            if (IsLeader)
                _logger.LogInformation("I am leader!");
            else
                _logger.LogInformation("{Address}:{Id} is leader!", leader?.Address, leader?.MemberId);
        }

        public void UpdateClusterTopology(IReadOnlyCollection<Member> statuses, ulong eventId)
        {
            
            var locked = _rwLock.TryEnterWriteLock(1000);

            while (!locked)
            {
                _logger.LogDebug("MemberList did not acquire writer lock within 1 seconds, retry");
                locked = _rwLock.TryEnterWriteLock(1000);
            }

            try
            {
                _logger.LogDebug("Updating Cluster Topology");
                
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

                var topology = new ClusterTopology
                {
                    EventId = newMembershipHashCode
                };

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

                _logger.LogDebug("Published ClusterTopology event {ClusterTopology}", topology);

                if (topology.Joined.Count > 0) _logger.LogInformation("Cluster members joined {MembersJoined}", topology.Joined);

                if (topology.Left.Count > 0) _logger.LogInformation("Cluster members left {MembersJoined}", topology.Left);

                _eventStream.Publish(topology);
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        private void MemberLeave(Member memberThatLeft)
        {
            //update MemberStrategy
            foreach (var k in memberThatLeft.Kinds)
            {
                if (!_memberStrategyByKind.TryGetValue(k, out var ms)) continue;

                ms.RemoveMember(memberThatLeft);

                if (ms.GetAllMembers().Count == 0) _memberStrategyByKind.Remove(k);
            }

            _bannedMembers.Add(memberThatLeft.Id);

            _members = _members.Remove(memberThatLeft.Id);
            _membersByIndex = _membersByIndex.Remove(memberThatLeft.Index);

            if (_indexByAddress.TryGetValue(memberThatLeft.Address, out var member) && memberThatLeft.Id.Equals(member))
                _indexByAddress = _indexByAddress.Remove(memberThatLeft.Address);

            var endpointTerminated = new EndpointTerminatedEvent {Address = memberThatLeft.Address};
            _logger.LogDebug("Published event {@EndpointTerminated}", endpointTerminated);
            _cluster.System.EventStream.Publish(endpointTerminated);
        }

        private void MemberJoin(Member newMember)
        {
            newMember.Index = _nextMemberIndex++;

            //TODO: looks fishy, no locks, are we sure this is safe? it is using private state _vars

            _members = _members.Add(newMember.Id, newMember);
            _membersByIndex = _membersByIndex.Add(newMember.Index, newMember);
            _indexByAddress = _indexByAddress.Add(newMember.Address, newMember.Index);

            foreach (var kind in newMember.Kinds)
            {
                if (!_memberStrategyByKind.ContainsKey(kind))
                {
                    _memberStrategyByKind = _memberStrategyByKind.SetItem(kind,
                        _cluster.Config!.MemberStrategyBuilder(_cluster, kind) ?? new SimpleMemberStrategy()
                    );
                }

                //TODO: this doesnt work, just use the same strategy for all kinds...
                _memberStrategyByKind[kind].AddMember(newMember);
            }
        }

        /// <summary>
        ///     broadcast a message to all members eventstream
        /// </summary>
        /// <param name="message"></param>
        public void BroadcastEvent(object message, bool includeSelf = true)
        {
            foreach (var (id, member) in _members.ToArray())
            {
                if (!includeSelf && id == _cluster.System.Id) continue;

                var pid = PID.FromAddress(member.Address, "eventstream");

                try
                {
                    _system.Root.Send(pid, message);
                }
                catch (Exception)
                {
                    _logger.LogError("Failed to broadcast {Message} to {Pid}", message, pid);
                }
            }
        }

        public bool ContainsMemberId(string memberId) => _members.ContainsKey(memberId);

        public bool TryGetMember(string memberId, out Member value) => _members.TryGetValue(memberId, out value);

        public bool TryGetMemberIndexByAddress(string address, out int value) => _indexByAddress.TryGetValue(address, out value);

        public bool TryGetMemberByIndex(int memberIndex, out Member value) => _membersByIndex.TryGetValue(memberIndex, out value);

        public Member[] GetAllMembers() => _members.Values.ToArray();
    }
}