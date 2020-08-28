// -----------------------------------------------------------------------
//   <copyright file="Partition.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Linq;

namespace Proto.Cluster.Partition
{
    //helper to interact with partition actors on this and other members
    internal class PartitionManager
    {
        internal const string PartitionIdentityActorName = "partition-actor";
        internal const string PartitionPlacementActorName = "partition-activator";
        private readonly Cluster _cluster;
        private readonly IRootContext _context;
        private readonly ActorSystem _system;
        private PID _partitionActivator = null!;
        private PID _partitionActor = null!;


        internal PartitionManager(Cluster cluster)
        {
            _cluster = cluster;
            _system = cluster.System;
            _context = _system.Root;
        }

        internal PartitionMemberSelector Selector { get; } = new PartitionMemberSelector();

        public void Setup()
        {
            var partitionActorProps = Props
                .FromProducer(() => new PartitionIdentityActor(_cluster, this))
                .WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy);
            _partitionActor = _context.SpawnNamed(partitionActorProps, PartitionIdentityActorName);

            var partitionActivatorProps =
                Props.FromProducer(() => new PartitionPlacementActor(_cluster, this));
            _partitionActivator = _context.SpawnNamed(partitionActivatorProps, PartitionPlacementActorName);

            //synchronous subscribe to keep accurate

            var eventId = 0ul;
            //make sure selector is updated first
            _system.EventStream.Subscribe<ClusterTopology>(e =>
                {
                    if (e.EventId > eventId)
                    {
                        eventId = e.EventId;
                        _cluster.MemberList.BroadcastEvent(e);

                        Selector.Update(e.Members.ToArray());
                        _context.Send(_partitionActor, e);
                        _context.Send(_partitionActivator, e);
                    }
                }
            );
        }


        public void Shutdown()
        {
            _context.Stop(_partitionActor);
            _context.Stop(_partitionActivator);
        }

        public PID RemotePartitionIdentityActor(string address) => new PID(address, PartitionIdentityActorName);

        public PID RemotePartitionPlacementActor(string address) => new PID(address, PartitionPlacementActorName);
    }
}