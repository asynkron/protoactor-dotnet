// -----------------------------------------------------------------------
//   <copyright file="MemberList.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Proto.Remote;

namespace Proto.Cluster
{
    //TODO: check usage and threadsafety.
    public class MemberList
    {
        private static readonly ILogger _logger = Log.CreateLogger<MemberList>();

        private readonly ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();
        private readonly Dictionary<Guid, MemberStatus> _members = new Dictionary<Guid, MemberStatus>();
        private readonly Dictionary<string, IMemberStrategy> _memberStrategyByKind = new Dictionary<string, IMemberStrategy>();
        private readonly Cluster _cluster;
        

        public MemberList(Cluster cluster) => _cluster = cluster;
        
        internal string GetMemberFromIdentityAndKind(string identity, string kind)
        {
            var locked = _rwLock.TryEnterReadLock(1000);

            while (!locked)
            {
                _logger.LogDebug($"{_cluster.Id} MemberList did not acquire reader lock within 1 seconds, retry");
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
                _logger.LogDebug($"{_cluster.Id}MemberList did not acquire reader lock within 1 seconds, retry");
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
                _logger.LogDebug($"{_cluster.Id} MemberList did not acquire writer lock within 1 seconds, retry");
                locked = _rwLock.TryEnterWriteLock(1000);
            }

            try
            {
                //get all new members id sets
                var newMemberIds = new HashSet<Guid>();

                foreach (var status in statuses)
                {
                    newMemberIds.Add(status.MemberId);
                }

                //remove old ones whose address not exist in new address sets
                //_members.ToList() duplicates _members, allow _members to be modified in Notify
                foreach (var (id, old) in _members.ToList())
                {
                    if (!newMemberIds.Contains(id))
                    {
                        UpdateAndNotify(null, old);
                    }
                }

                //find all the entries that exist in the new set
                foreach (var @new in statuses)
                {
                    _members.TryGetValue(@new.MemberId, out var old);
                    _members[@new.MemberId] = @new;
                    UpdateAndNotify(@new, old);
                }
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        private void UpdateAndNotify(MemberStatus? newMember, MemberStatus? oldMember)
        {
            //TODO: looks fishy, no locks, are we sure this is safe? it is using private state _vars



            if (newMember == null)
            {
                if (oldMember == null)
                {
                    return; //ignore
                }

                //update MemberStrategy
                foreach (var k in oldMember.Kinds)
                {
                    if (!_memberStrategyByKind.TryGetValue(k, out var ms)) continue;

                    ms.RemoveMember(oldMember);

                    if (ms.GetAllMembers().Count == 0) _memberStrategyByKind.Remove(k);
                }

                //notify left
                var left = new MemberLeftEvent(oldMember.MemberId, oldMember.Host, oldMember.Port, oldMember.Kinds);
                _cluster.System.EventStream.Publish(left);
                _logger.LogInformation($"{_cluster.Id} Published event {left}");
                
                _cluster.PidCache.RemoveByMemberAddress($"{oldMember.Host}:{oldMember.Port}");
                _members.Remove(oldMember.MemberId);

                var endpointTerminated = new EndpointTerminatedEvent {Address = oldMember.Address};
                _cluster.System.EventStream.Publish(endpointTerminated);
                _logger.LogInformation($"{_cluster.Id} Published event {endpointTerminated}");

                return;
            }

            if (oldMember == null)
            {
                foreach (var k in newMember.Kinds)
                {
                    if (!_memberStrategyByKind.ContainsKey(k)) _memberStrategyByKind[k] = _cluster.Config!.MemberStrategyBuilder(k);
                    _memberStrategyByKind[k].AddMember(newMember);
                }

                //notify joined
                var joined = new MemberJoinedEvent(newMember.MemberId, newMember.Host, newMember.Port, newMember.Kinds);
                _cluster.System.EventStream.Publish(joined);
                _logger.LogInformation($"{_cluster.Id} Published event {joined}");
                
                
                _cluster.PidCache.RemoveByMemberAddress($"{newMember.Host}:{newMember.Port}");

             
            }
        }
    }
}
