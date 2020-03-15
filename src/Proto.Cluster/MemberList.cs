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

        private readonly ReaderWriterLockSlim RwLock = new ReaderWriterLockSlim();
        private readonly Dictionary<string, MemberStatus> Members = new Dictionary<string, MemberStatus>();
        private readonly Dictionary<string, IMemberStrategy> MemberStrategyByKind = new Dictionary<string, IMemberStrategy>();
        private readonly Cluster Cluster;
        private Subscription<object> clusterTopologyEvnSub;

        public MemberList(Cluster cluster)
        {
            Cluster = cluster;
        }

        internal void Setup() => clusterTopologyEvnSub = Cluster.System.EventStream.Subscribe<ClusterTopologyEvent>(UpdateClusterTopology);

        internal void Stop() => Cluster.System.EventStream.Unsubscribe(clusterTopologyEvnSub.Id);

        internal string GetPartition(string name, string kind)
        {
            var locked = RwLock.TryEnterReadLock(1000);

            while (!locked)
            {
                Logger.LogDebug("MemberList did not acquire reader lock within 1 seconds, retry");
                locked = RwLock.TryEnterReadLock(1000);
            }

            try
            {
                return MemberStrategyByKind.TryGetValue(kind, out var memberStrategy)
                    ? memberStrategy.GetPartition(name)
                    : "";
            }
            finally
            {
                RwLock.ExitReadLock();
            }
        }

        internal string GetActivator(string kind)
        {
            var locked = RwLock.TryEnterReadLock(1000);

            while (!locked)
            {
                Logger.LogDebug("MemberList did not acquire reader lock within 1 seconds, retry");
                locked = RwLock.TryEnterReadLock(1000);
            }

            try
            {
                return MemberStrategyByKind.TryGetValue(kind, out var memberStrategy)
                    ? memberStrategy.GetActivator()
                    : "";
            }
            finally
            {
                RwLock.ExitReadLock();
            }
        }

        internal void UpdateClusterTopology(ClusterTopologyEvent msg)
        {
            var locked = RwLock.TryEnterWriteLock(1000);

            while (!locked)
            {
                Logger.LogDebug("MemberList did not acquire writer lock within 1 seconds, retry");
                locked = RwLock.TryEnterWriteLock(1000);
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
                foreach (var (address, old) in Members.ToList())
                {
                    if (!newMembersAddress.Contains(address))
                    {
                        UpdateAndNotify(null, old);
                    }
                }

                //find all the entries that exist in the new set
                foreach (var @new in msg.Statuses)
                {
                    Members.TryGetValue(@new.Address, out var old);
                    Members[@new.Address] = @new;
                    UpdateAndNotify(@new, old);
                }
            }
            finally
            {
                RwLock.ExitWriteLock();
            }
        }

        private void UpdateAndNotify(MemberStatus @new, MemberStatus old)
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
                    if (MemberStrategyByKind.TryGetValue(k, out var ms))
                    {
                        ms.RemoveMember(old);

                        if (ms.GetAllMembers().Count == 0)
                            MemberStrategyByKind.Remove(k);
                    }
                }

                //notify left
                var left = new MemberLeftEvent(old.Host, old.Port, old.Kinds);
                Cluster.System.EventStream.Publish(left);

                Members.Remove(old.Address);

                var endpointTerminated = new EndpointTerminatedEvent { Address = old.Address };
                Cluster.System.EventStream.Publish(endpointTerminated);

                return;
            }

            if (old == null)
            {
                //update MemberStrategy
                foreach (var k in @new.Kinds)
                {
                    if (!MemberStrategyByKind.ContainsKey(k))
                        MemberStrategyByKind[k] = Cluster.Config.MemberStrategyBuilder(k);
                    MemberStrategyByKind[k].AddMember(@new);
                }

                //notify joined
                var joined = new MemberJoinedEvent(@new.Host, @new.Port, @new.Kinds);
                Cluster.System.EventStream.Publish(joined);

                return;
            }

            //update MemberStrategy
            if (@new.Alive != old.Alive || @new.MemberId != old.MemberId || @new.StatusValue != null && !@new.StatusValue.IsSame(old.StatusValue))
            {
                foreach (var k in @new.Kinds)
                {
                    if (MemberStrategyByKind.TryGetValue(k, out var ms))
                    {
                        ms.UpdateMember(@new);
                    }
                }
            }

            //notify changes
            if (@new.MemberId != old.MemberId)
            {
                var rejoined = new MemberRejoinedEvent(@new.Host, @new.Port, @new.Kinds);
                Cluster.System.EventStream.Publish(rejoined);
            }
        }
    }
}