// -----------------------------------------------------------------------
//   <copyright file="Partition.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Proto.Cluster
{
    //This class keeps track of all partitions. 
    //it spawns one partition actor per known kind
    //then  each partition actor PID is stored in a lookup
    internal class PartitionManager
    {
        private readonly ConcurrentDictionary<string, PID> _kindMap = new ConcurrentDictionary<string, PID>();

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
                SpawnPartitionActor(kind);
            }

            //TODO: should we have some other form of notification here?
            _memberStatusSub = _cluster.System.EventStream.Subscribe<MemberStatusEvent>(
                msg =>
                {
                    foreach (var kind in msg.Kinds)
                    {
                        //spawn if not existing
                        if (!_kindMap.TryGetValue(kind, out _))
                        {
                            SpawnPartitionActor(kind);
                        }

                        var kindPid = _kindMap[kind];
                        _cluster.System.Root.Send(kindPid, msg);
                        
                    }
                }
            );
        }

        private void SpawnPartitionActor(string kind)
        {
            var props = Props
                .FromProducer(() => new PartitionActor(_cluster, kind, this))
                .WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy);
            var pid = _cluster.System.Root.SpawnNamed(props, "partition-" + kind);
            _kindMap[kind] = pid;
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

        public PID PartitionForKind(string kind)
        {
            for (var i = 0; i < 5; i++)
            {
                if (_kindMap.TryGetValue(kind, out var pid))
                {
                    return pid;
                }
                Thread.Sleep(500);
            }
            
            throw new ArgumentException($"Tried to get partition for kind '{kind}', but none was found");
        }
    }
}