// -----------------------------------------------------------------------
//   <copyright file="MemberList.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Proto.Remote;

namespace Proto.Cluster
{
    public class MemberList
    {
        private static readonly ILogger Logger = Log.CreateLogger("MemberList");

        private readonly ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();
        private readonly Dictionary<string, MemberStatus> _members = new Dictionary<string, MemberStatus>();
        private readonly Dictionary<string, IMemberStrategy> _memberStrategyByKind = new Dictionary<string, IMemberStrategy>();
        private readonly Cluster _cluster;

        private Subscription<object>? _clusterTopologyEvnSub;

        public MemberList(Cluster cluster) => _cluster = cluster;

        internal int MembersCount => _members.Count;

        internal void Setup() => _clusterTopologyEvnSub = _cluster.System.EventStream.Subscribe<ClusterTopologyEvent>(UpdateClusterTopology);

        internal void Stop() => _cluster.System.EventStream.Unsubscribe(_clusterTopologyEvnSub);

        internal bool IsMember(string address) => _members.ContainsKey(address);

        internal string GetPartition(string name, string kind)
        {
            var locked = _rwLock.TryEnterReadLock(1000);

            while (!locked)
            {
                Logger.LogDebug("MemberList did not acquire reader lock within 1 seconds, retry");
                locked = _rwLock.TryEnterReadLock(1000);
            }

            try
            {
                return _memberStrategyByKind.TryGetValue(kind, out var memberStrategy)
                    ? memberStrategy.GetPartition(name)
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
                Logger.LogDebug("MemberList did not acquire reader lock within 1 seconds, retry");
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

        private void UpdateClusterTopology(ClusterTopologyEvent msg)
        {
            var locked = _rwLock.TryEnterWriteLock(1000);

            while (!locked)
            {
                Logger.LogDebug("MemberList did not acquire writer lock within 1 seconds, retry");
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

        private void UpdateAndNotify(MemberStatus? @new, MemberStatus? old)
        {
            // Make sure that only Alive members are considered valid.
            // This makes sure that the Members lists only contain alive nodes.
            var oldMember = old == null || old.Alive == false ? null : old;
            var newMember = @new == null || @new.Alive == false ? null : @new;

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
                var left = new MemberLeftEvent(oldMember.Host, oldMember.Port, oldMember.Kinds);
                _cluster.System.EventStream.Publish(left);

                _members.Remove(oldMember.Address);

                var endpointTerminated = new EndpointTerminatedEvent {Address = oldMember.Address};
                _cluster.System.EventStream.Publish(endpointTerminated);

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
                var joined = new MemberJoinedEvent(newMember.Host, newMember.Port, newMember.Kinds);
                _cluster.System.EventStream.Publish(joined);

                return;
            }

            //update MemberStrategy
            if (newMember.Alive != oldMember.Alive || newMember.MemberId != oldMember.MemberId ||
                newMember.StatusValue != null && !newMember.StatusValue.IsSame(oldMember.StatusValue))
            {
                foreach (var k in newMember.Kinds)
                {
                    if (_memberStrategyByKind.TryGetValue(k, out var ms))
                    {
                        ms.UpdateMember(newMember);
                    }
                }
            }

            //notify changes
            if (newMember.MemberId != oldMember.MemberId)
            {
                var rejoined = new MemberRejoinedEvent(newMember.Host, newMember.Port, newMember.Kinds);
                _cluster.System.EventStream.Publish(rejoined);
            }
        }
    }
}
