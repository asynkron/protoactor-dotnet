// -----------------------------------------------------------------------
//   <copyright file="MemberList.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Proto.Remote;

namespace Proto.Cluster
{
    public static class MemberList
    {
        private static readonly ILogger _logger = Log.CreateLogger("MemberList");

		private static readonly ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();
        private static readonly Dictionary<string, MemberStatus> _members = new Dictionary<string, MemberStatus>();
        private static readonly Dictionary<string, IMemberStrategy> _memberStrategyByKind = new Dictionary<string, IMemberStrategy>();

        private static Subscription<object> _clusterTopologyEvnSub;

        internal static void SubscribeToEventStream()
        {
            _clusterTopologyEvnSub = Actor.EventStream.Subscribe<ClusterTopologyEvent>(UpdateClusterTopology);
        }

        internal static void UnsubEventStream()
        {
            Actor.EventStream.Unsubscribe(_clusterTopologyEvnSub.Id);
        }

        internal static string[] GetMembers(string kind)
        {
            bool locked = _rwLock.TryEnterReadLock(1000);
            while (!locked)
            {
                _logger.LogDebug("MemberList did not acquire reader lock within 1 seconds, retry");
                locked = _rwLock.TryEnterReadLock(1000);
            }

            try
            {
                return _memberStrategyByKind.TryGetValue(kind, out var memberStrategy)
                           ? memberStrategy.GetAllMembers().FindAll(m => m.Alive).Select(m => m.Address).ToArray() : new string[0];
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        internal static string GetPartition(string name, string kind)
        {
            bool locked = _rwLock.TryEnterReadLock(1000);
            while (!locked)
            {
                _logger.LogDebug("MemberList did not acquire reader lock within 1 seconds, retry");
                locked = _rwLock.TryEnterReadLock(1000);
            }

            try
            {
                return _memberStrategyByKind.TryGetValue(kind, out var memberStrategy)
                           ? memberStrategy.GetPartition(name) : "";
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        internal static string GetActivator(string kind)
        {
            bool locked = _rwLock.TryEnterReadLock(1000);
            while (!locked)
            {
                _logger.LogDebug("MemberList did not acquire reader lock within 1 seconds, retry");
                locked = _rwLock.TryEnterReadLock(1000);
            }

            try
            {
                return _memberStrategyByKind.TryGetValue(kind, out var memberStrategy)
                           ? memberStrategy.GetActivator() : "";
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        internal static void UpdateClusterTopology(ClusterTopologyEvent msg)
        {
            bool locked = _rwLock.TryEnterWriteLock(1000);
            while (!locked)
            {
                _logger.LogDebug("MemberList did not acquire writer lock within 1 seconds, retry");
                locked = _rwLock.TryEnterWriteLock(1000);
            }

            try
            {
                //get all new members address sets
                var newMembersAddress = new HashSet<string>();
                foreach (var status in msg.Statuses)
                {
                    newMembersAddress.Add(status.Address);
                }

                //remove old ones whose address not exist in new address sets
                //_members.ToList() duplicates _members, allow _members to be modified in Notify
                foreach (var (address, old) in _members.ToList())
                {
                    if (!newMembersAddress.Contains(address))
                    {
                        UpdateAndNotify(null, old);
                    }
                }

                //find all the entries that exist in the new set
                foreach (var @new in msg.Statuses)
                {
                    _members.TryGetValue(@new.Address, out var old);
                    _members[@new.Address] = @new;
                    UpdateAndNotify(@new, old);
                }
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        private static void UpdateAndNotify(MemberStatus @new, MemberStatus old)
        {
            if (@new == null && old == null)
            {
                return; //ignore
            }

            if (@new == null)
            {
                //update MemberStrategy
                foreach (var k in old.Kinds)
                {
                    if (_memberStrategyByKind.TryGetValue(k, out var ms))
                    {
                        ms.RemoveMember(old);
                        if (ms.GetAllMembers().Count == 0)
                            _memberStrategyByKind.Remove(k);
                    }
                }

                //notify left
                var left = new MemberLeftEvent(old.Host, old.Port, old.Kinds);
                Actor.EventStream.Publish(left);
                _members.Remove(old.Address);
                var endpointTerminated = new EndpointTerminatedEvent
                {
                    Address = old.Address
                };
                Actor.EventStream.Publish(endpointTerminated);
                return;
            }

            if (old == null)
            {
                //update MemberStrategy
                foreach (var k in @new.Kinds)
                {
                    if (!_memberStrategyByKind.ContainsKey(k))
                        _memberStrategyByKind[k] = Cluster.cfg.MemberStrategyBuilder(k);
                    _memberStrategyByKind[k].AddMember(@new);
                }

                //notify joined
                var joined = new MemberJoinedEvent(@new.Host, @new.Port, @new.Kinds);
                Actor.EventStream.Publish(joined);
                return;
            }

            //update MemberStrategy
            if (@new.Alive != old.Alive || @new.MemberId != old.MemberId || @new.StatusValue != null && !@new.StatusValue.IsSame(old.StatusValue))
            {
                foreach (var k in @new.Kinds)
                {
                    if (_memberStrategyByKind.TryGetValue(k, out var ms))
                    {
                        ms.UpdateMember(@new);
                    }
                }
            }

            //notify changes
            if (@new.MemberId != old.MemberId)
            {
                var rejoined = new MemberRejoinedEvent(@new.Host, @new.Port, @new.Kinds);
                Actor.EventStream.Publish(rejoined);
                return;
            }
        }
    }
}