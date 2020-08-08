// -----------------------------------------------------------------------
//   <copyright file="Partition.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;

namespace Proto.Cluster
{
    //This class keeps track of all partitions. 
    //it spawns one partition actor per known kind
    //then  each partition actor PID is stored in a lookup
    internal class PartitionManager
    {
        private readonly Dictionary<string, PID> _kindMap = new Dictionary<string, PID>();

        private Subscription<object>? _memberStatusSub;
        private readonly Cluster _cluster;

        internal PartitionManager(Cluster cluster)
        {
            _cluster = cluster;
        }

        public void Setup(string[] kinds)
        {
            foreach (var kind in kinds)
            {
                var pid = SpawnPartitionActor(kind);
                _kindMap[kind] = pid;
            }

            //TODO: should we have some other form of notification here?
            _memberStatusSub = _cluster.System.EventStream.Subscribe<MemberStatusEvent>(
                msg =>
                {
                    foreach (var kind in msg.Kinds)
                    {
                        if (_kindMap.TryGetValue(kind, out var kindPid))
                        {
                            _cluster.System.Root.Send(kindPid, msg);
                        }
                    }
                }
            );
        }

        private PID SpawnPartitionActor(string kind)
        {
            var props = Props
                .FromProducer(() => new PartitionActor(_cluster, kind, this))
                .WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy);
            var pid = _cluster.System.Root.SpawnNamed(props, "partition-" + kind);
            return pid;
        }

        public void Stop()
        {
            foreach (var kind in _kindMap.Values)
            {
                _cluster.System.Root.Stop(kind);
            }

            _kindMap.Clear();
            _cluster.System.EventStream.Unsubscribe(_memberStatusSub);
        }

        public PID PartitionForKind(string address, string kind)
        {
            return _kindMap[kind];
        }
    }
}