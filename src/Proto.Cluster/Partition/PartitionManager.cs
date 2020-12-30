// -----------------------------------------------------------------------
// <copyright file="PartitionManager.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Linq;

namespace Proto.Cluster.Partition
{
    //helper to interact with partition actors on this and other members
    class PartitionManager
    {
        private const string PartitionIdentityActorName = "partition-identity";
        private const string PartitionPlacementActorName = "partition-activator";
        private readonly Cluster _cluster;
        private readonly IRootContext _context;
        private readonly bool _isClient;
        private readonly ActorSystem _system;
        private PID _partitionActivator = null!;
        private PID _partitionActor = null!;

        internal PartitionManager(Cluster cluster, bool isClient)
        {
            _cluster = cluster;
            _system = cluster.System;
            _context = _system.Root;
            _isClient = isClient;
        }

        internal PartitionMemberSelector Selector { get; } = new();

        public void Setup()
        {
            if (_isClient)
            {
                var eventId = 0ul;
                //make sure selector is updated first
                _system.EventStream.Subscribe<ClusterTopology>(e => {
                        if (e.EventId > eventId)
                        {
                            eventId = e.EventId;
                            Selector.Update(e.Members.ToArray());
                        }
                    }
                );
            }
            else
            {
                var partitionActorProps = Props
                    .FromProducer(() => new PartitionIdentityActor(_cluster))
                    .WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy);
                _partitionActor = _context.SpawnNamed(partitionActorProps, PartitionIdentityActorName);

                var partitionActivatorProps =
                    Props.FromProducer(() => new PartitionPlacementActor(_cluster));
                _partitionActivator = _context.SpawnNamed(partitionActivatorProps, PartitionPlacementActorName);

                //synchronous subscribe to keep accurate

                var eventId = 0ul;
                //make sure selector is updated first
                _system.EventStream.Subscribe<ClusterTopology>(e => {
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
        }

        public void Shutdown()
        {
            if (_isClient)
            {
            }
            else
            {
                _context.Stop(_partitionActor);
                _context.Stop(_partitionActivator);
            }
        }

        public static PID RemotePartitionIdentityActor(string address) =>
            PID.FromAddress(address, PartitionIdentityActorName);

        public static PID RemotePartitionPlacementActor(string address) =>
            PID.FromAddress(address, PartitionPlacementActorName);
    }
}