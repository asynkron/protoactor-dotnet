// -----------------------------------------------------------------------
// <copyright file="MemberList.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Proto.Remote;

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

        private readonly IRootContext _root;
        private readonly ActorSystem _system;
        private ImmutableDictionary<string, int> _indexByAddress = ImmutableDictionary<string, int>.Empty;
        private TaskCompletionSource<bool> _topologyConsensus = new ();
        private ImmutableDictionary<string,MetaMember> _metaMembers = ImmutableDictionary<string, MetaMember>.Empty;

        private Member? _leader;

        //TODO: the members here are only from the cluster provider
        //The partition lookup broadcasts and use broadcasted information
        //meaning the partition infra might be ahead of this list.
        //come up with a good solution to keep all this in sync
        private ImmutableMemberSet _members = ImmutableMemberSet.Empty;
        private ImmutableMemberSet _bannedMembers = ImmutableMemberSet.Empty;
        
        private ImmutableDictionary<int, Member> _membersByIndex = ImmutableDictionary<int, Member>.Empty;

        private ImmutableDictionary<string, IMemberStrategy> _memberStrategyByKind = ImmutableDictionary<string, IMemberStrategy>.Empty;

        private int _nextMemberIndex;

        public MemberList(Cluster cluster)
        {
            _cluster = cluster;
            _system = _cluster.System;
            _root = _system.Root;
            _eventStream = _system.EventStream;
        }

        public ImmutableHashSet<string> GetMembers() => _members.Members.Select(m=>m.Id).ToImmutableHashSet();

        public Task TopologyConsensus() => _topologyConsensus.Task;

        public Member? GetActivator(string kind, string requestSourceAddress)
        {
            lock (this)
            {
                if (_memberStrategyByKind.TryGetValue(kind, out var memberStrategy))
                    return memberStrategy.GetActivator(requestSourceAddress);

                Logger.LogInformation("MemberList did not find any not find any activator for kind '{Kind}'", kind);
                return null;
            }
        }

        public void UpdateClusterTopology(IReadOnlyCollection<Member> members)
        {
            lock (this)
            {
                Logger.LogDebug("[MemberList] Updating Cluster Topology");

                //TLDR:
                //this method basically filters out any member status in the banned list
                //then makes a delta between new and old members
                //notifying the cluster accordingly which members left or joined

                var activeMembers = new ImmutableMemberSet(members).Except(_bannedMembers);
                if (activeMembers.Equals(_members))
                {
                    return;
                }

                var left = _members.Except(activeMembers);
                var joined = activeMembers.Except(_members);
                _bannedMembers = _bannedMembers.Union(left);
                _members = activeMembers;
                
                //notify that these members left
                foreach (var memberThatLeft in left.Members)
                {
                    MemberLeave(memberThatLeft);
                    TerminateMember(memberThatLeft);
                }
                
                //notify that these members joined
                foreach (var memberThatJoined in joined.Members)
                {
                    MemberJoin(memberThatJoined);
                }
                
                var topology = new ClusterTopology
                {
                    TopologyHash = activeMembers.TopologyHash,
                    Members = {activeMembers.Members},
                    Left = {left.Members},
                    Joined = {joined.Members}
                };
                
                Logger.LogDebug("[MemberList] Published ClusterTopology event {ClusterTopology}", topology);
                
                if (topology.Joined.Any()) Logger.LogInformation("[MemberList] Cluster members joined {MembersJoined}", topology.Joined);

                if (topology.Left.Any()) Logger.LogInformation("[MemberList] Cluster members left {MembersJoined}", topology.Left);
                
                BroadcastTopologyChanges(topology);
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

                if (_metaMembers.TryGetValue(memberThatLeft.Id, out var meta))
                {
                    _membersByIndex = _membersByIndex.Remove(meta.Index);

                    if (_indexByAddress.TryGetValue(memberThatLeft.Address, out _))
                        _indexByAddress = _indexByAddress.Remove(memberThatLeft.Address);
                }
                else
                {
                    //Log?
                }
            }

            void MemberJoin(Member newMember)
            {
                var index = _nextMemberIndex++;
                _metaMembers = _metaMembers.Add(newMember.Id, new MetaMember(newMember, index));
                _membersByIndex = _membersByIndex.Add(index, newMember);
                _indexByAddress = _indexByAddress.Add(newMember.Address, index);

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

        public MetaMember? GetMetaMember(string memberId)
        {
            _metaMembers.TryGetValue(memberId, out var meta);
            return meta;
        }

        private void BroadcastTopologyChanges(ClusterTopology topology)
        {
            _eventStream.Publish(topology);
            _cluster.Gossip.SetState("topology", topology);
            //Console.WriteLine($"{_system.Id} Broadcasting {topology.TopologyHash} - {topology.Members.Count}");
        }

        private void TerminateMember(Member memberThatLeft)
        {
            var endpointTerminated = new EndpointTerminatedEvent {Address = memberThatLeft.Address};
            Logger.LogDebug("[MemberList] Published event {@EndpointTerminated}", endpointTerminated);
            _cluster.System.EventStream.Publish(endpointTerminated);
        }

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
            foreach (var (id, member) in _members.Lookup)
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

        public bool ContainsMemberId(string memberId) => _members.Contains(memberId);

        public bool TryGetMember(string memberId, out Member? value) => _members.Lookup.TryGetValue(memberId, out value);

        public bool TryGetMemberIndexByAddress(string address, out int value) => _indexByAddress.TryGetValue(address, out value);

        public bool TryGetMemberByIndex(int memberIndex, out Member? value) => _membersByIndex.TryGetValue(memberIndex, out value);

        public Member[] GetAllMembers() => _members.Members.ToArray();
        public Member[] GetOtherMembers() => _members.Members.Where(m => m.Id != _system.Id).ToArray();

        internal void TrySetTopologyConsensus()
        {
            //if not set, set it, if already set, keep it set
            _topologyConsensus.TrySetResult(true);
        }

        public void TryResetTopologyConsensus()
        {
            //only replace if the task is completed
            if (_topologyConsensus.Task.IsCompleted)
            {
                _topologyConsensus = new TaskCompletionSource<bool>();
            }
        }
    }
}