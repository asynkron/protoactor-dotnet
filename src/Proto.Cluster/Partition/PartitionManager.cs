// -----------------------------------------------------------------------
//   <copyright file="Partition.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

namespace Proto.Cluster.Partition
{
    //helper to interact with partition actors on this and other members
    internal class PartitionManager
    {
        private PID _actor = null!;
        private readonly Cluster _cluster;
        internal PartitionMemberSelector Selector { get; } = new PartitionMemberSelector();


        internal PartitionManager(Cluster cluster)
        {
            _cluster = cluster;
        }

        public void Setup()
        {
            SpawnPartitionActor();
        }

        private void SpawnPartitionActor()
        {
            _cluster.System.EventStream.Subscribe<MemberStatusEvent>(e =>
                {
                    _cluster.System.Root.Send(_actor,e);
                }
            );
            
            var props = Props
                .FromProducer(() => new PartitionActor(_cluster, this))
                .WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy);
             _actor = _cluster.System.Root.SpawnNamed(props, "partition-actor");
        }

        public void Stop()
        {
            _cluster.System.Root.Stop(_actor);
        }

        public PID RemotePartitionForKind(string address)
        {
            return new PID(address, "partition-actor");
        }
    }
}