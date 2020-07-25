// -----------------------------------------------------------------------
//   <copyright file="Partition.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;

namespace Proto.Cluster
{
    internal class Partition
    {
        private readonly Dictionary<string, PID> _kindMap = new Dictionary<string, PID>();

        private Subscription<object>? _memberStatusSub;
        private Cluster Cluster { get; }

        internal Partition(Cluster cluster) => Cluster = cluster;

        public void Setup(string[] kinds)
        {
            foreach (var kind in kinds)
            {
                var pid = SpawnPartitionActor(kind);
                _kindMap[kind] = pid;
            }

            _memberStatusSub = Cluster.System.EventStream.Subscribe<MemberStatusEvent>(
                msg =>
                {
                    foreach (var kind in msg.Kinds)
                    {
                        if (_kindMap.TryGetValue(kind, out var kindPid))
                        {
                            Cluster.System.Root.Send(kindPid, msg);
                        }
                    }
                }
            );
        }

        private PID SpawnPartitionActor(string kind)
        {
            var props = Props
                .FromProducer(() => new PartitionActor(Cluster, kind))
                .WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy);
            var pid = Cluster.System.Root.SpawnNamed(props, "partition-" + kind);
            return pid;
        }

        public void Stop()
        {
            foreach (var kind in _kindMap.Values)
            {
                Cluster.System.Root.Stop(kind);
            }

            _kindMap.Clear();
            Cluster.System.EventStream.Unsubscribe(_memberStatusSub);
        }

        public PID PartitionForKind(string address, string kind) => new PID(address, "partition-" + kind);
    }
}
