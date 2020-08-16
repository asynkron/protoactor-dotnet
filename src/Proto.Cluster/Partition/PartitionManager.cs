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
        private PID _partitionActor = null!;
        private PID _partitionActivator = null!;
        private readonly Cluster _cluster;
        private readonly ActorSystem _system;
        private readonly IRootContext _context;
        internal PartitionMemberSelector Selector { get; } = new PartitionMemberSelector();


        internal PartitionManager(Cluster cluster)
        {
            _cluster = cluster;
            _system = cluster.System;
            _context = _system.Root;
        }

        public void Setup()
        {
            var partitionActorProps = Props
                .FromProducer(() => new PartitionActor(_cluster, this))
                .WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy);
            _partitionActor = _context.SpawnNamed(partitionActorProps, "partition-actor");
            
            var partitionActivatorProps =
                Props.FromProducer(() => new PartitionActivator(_cluster.Remote, _cluster.System));
            _partitionActivator = _context.SpawnNamed(partitionActivatorProps, "partition-activator");

            _system.EventStream.Subscribe<MemberStatusEvent>(_context, 
                _partitionActor,
                _partitionActivator
            );
        }


        public void Shutdown()
        {
            _context.Stop(_partitionActor);
            _context.Stop(_partitionActivator);
        }

        public PID RemotePartitionForKind(string address)
        {
            return new PID(address, "partition-actor");
        }
    }
}