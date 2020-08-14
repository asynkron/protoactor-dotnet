// -----------------------------------------------------------------------
//   <copyright file="MemberList.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Proto.Cluster.Utils;
using Proto.Remote;

namespace Proto.Cluster
{
    //This class is responsible for figuring out what members are currently active in the cluster
    //it will receive a list of memberstatuses from the IClusterProvider
    //from that, we calculate a delta, which members joined, or left.

    //TODO: check usage and threadsafety.
    public class MemberList
    {
        private static ILogger _logger = null!;

        //TODO: actually use this to prevent banned members from rejoining
        private readonly ConcurrentSet<Guid> _bannedMembers = new ConcurrentSet<Guid>();
        private readonly Cluster _cluster;
        private readonly Dictionary<Guid, MemberStatus> _members = new Dictionary<Guid, MemberStatus>();

        private readonly Dictionary<string, IMemberStrategy> _memberStrategyByKind =
            new Dictionary<string, IMemberStrategy>();

        private readonly ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();

        public MemberList(Cluster cluster)
        {
            _cluster = cluster;
            _logger = Log.CreateLogger($"MemberList-{_cluster.Id}");
        }

        internal string GetMemberFromIdentityAndKind(string identity, string kind)
        {
            var locked = _rwLock.TryEnterReadLock(1000);

            while (!locked)
            {
                _logger.LogDebug("MemberList did not acquire reader lock within 1 seconds, retry");
                locked = _rwLock.TryEnterReadLock(1000);
            }

            try
            {
                return _memberStrategyByKind.TryGetValue(kind, out var memberStrategy)
                    ? memberStrategy.GetPartition(identity)
                    : "";
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        internal string GetActivator(string kind)
        {
            var locked = _rwLock.TryEnterReadLock(1000);

            while (!locked)
            {
                _logger.LogDebug("MemberList did not acquire reader lock within 1 seconds, retry");
                locked = _rwLock.TryEnterReadLock(1000);
            }

            try
            {
                return _memberStrategyByKind.TryGetValue(kind, out var memberStrategy)
                    ? memberStrategy.GetActivator()
                    : "";
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        public void UpdateClusterTopology(IReadOnlyCollection<MemberStatus> statuses)
        {
            var locked = _rwLock.TryEnterWriteLock(1000);

            while (!locked)
            {
                _logger.LogDebug("MemberList did not acquire writer lock within 1 seconds, retry");
                locked = _rwLock.TryEnterWriteLock(1000);
            }

            try
            {
                //TLDR:
                //this method basically filters out any member status in the banned list
                //then makes a delta between new and old members
                //notifying the cluster accordingly which members left or joined

                //these are all members that are currently active
                var nonBannedStatuses =
                    statuses
                        .Where(s => !_bannedMembers.Contains(s.MemberId))
                        .ToArray();

                //these are the member IDs hashset of currently active members
                var newMemberIds =
                    nonBannedStatuses
                        .Select(s => s.MemberId)
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
                }

                //these are all members that are new and did not exist before
                var membersThatJoined =
                    nonBannedStatuses
                        .Where(m => !_members.ContainsKey(m.MemberId))
                        .ToArray();

                //notify that these members joined
                foreach (var memberThatJoined in membersThatJoined)
                {
                    MemberJoin(memberThatJoined);
                }
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        private void MemberLeave(MemberStatus memberThatLeft)
        {
            //update MemberStrategy
            foreach (var k in memberThatLeft.Kinds)
            {
                if (!_memberStrategyByKind.TryGetValue(k, out var ms))
                {
                    continue;
                }

                ms.RemoveMember(memberThatLeft);

                if (ms.GetAllMembers().Count == 0)
                {
                    _memberStrategyByKind.Remove(k);
                }
            }

            //notify left

            var left = new MemberLeftEvent(memberThatLeft.MemberId, memberThatLeft.Host, memberThatLeft.Port,
                memberThatLeft.Kinds
            );

            //remember that this member has left, may never join cluster again
            //that is, this ID may never join again, any cluster on the same host and port is fine
            //as long as it is a new clean instance
            _bannedMembers.Add(left.Id);
            _logger.LogInformation("Published event {@MemberLeft}",left);
            _cluster.System.EventStream.Publish(left);


            _cluster.PidCache.RemoveByMemberAddress($"{memberThatLeft.Host}:{memberThatLeft.Port}");
            _members.Remove(memberThatLeft.MemberId);

            var endpointTerminated = new EndpointTerminatedEvent {Address = memberThatLeft.Address};
            _logger.LogInformation("Published event {@EndpointTerminated}",endpointTerminated);
            _cluster.System.EventStream.Publish(endpointTerminated);
        }

        private void MemberJoin(MemberStatus newMember)
        {
            //TODO: looks fishy, no locks, are we sure this is safe? it is using private state _vars

            _members.Add(newMember.MemberId, newMember);

            foreach (var kind in newMember.Kinds)
            {
                if (!_memberStrategyByKind.ContainsKey(kind))
                {
                    _memberStrategyByKind[kind] = _cluster.Config!.MemberStrategyBuilder(kind);
                }

                _memberStrategyByKind[kind].AddMember(newMember);
            }

            //notify joined
            var joined = new MemberJoinedEvent(newMember.MemberId, newMember.Host, newMember.Port, newMember.Kinds);

            _logger.LogInformation("Published event {@MemberJoined}",joined);
            _cluster.System.EventStream.Publish(joined);

            _cluster.PidCache.RemoveByMemberAddress($"{newMember.Host}:{newMember.Port}");
        }
    }
}