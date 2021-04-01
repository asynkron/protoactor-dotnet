// -----------------------------------------------------------------------
// <copyright file="PartitionManager.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;

namespace Proto.Cluster.Partition
{
    //helper to interact with partition actors on this and other members
    internal class PartitionManager
    {
        private const string PartitionIdentityActorName = "partition-identity";
        private const string PartitionPlacementActorName = "partition-activator";
        private readonly Cluster _cluster;
        private readonly IRootContext _context;
        private readonly TimeSpan _identityHandoverTimeout;
        private readonly bool _isClient;
        private readonly ActorSystem _system;
        private PID _partitionIdentityActor = null!;
        private PID _partitionPlacementActor = null!;

        internal PartitionManager(Cluster cluster, bool isClient, TimeSpan identityHandoverTimeout)
        {
            _cluster = cluster;
            _system = cluster.System;
            _context = _system.Root;
            _isClient = isClient;
            _identityHandoverTimeout = identityHandoverTimeout;
        }

        internal PartitionMemberSelector Selector { get; } = new();

        public void Setup()
        {
            if (_isClient)
            {
                ulong eventId = 0ul;
                //make sure selector is updated first
                _system.EventStream.Subscribe<ClusterTopology>(e =>
                    {
                        if (e.EventId == eventId)
                        {
                            return;
                        }

                        eventId = e.EventId;
                        Selector.Update(e.Members.ToArray());
                    }
                );
            }
            else
            {
                Props? partitionActorProps = Props
                    .FromProducer(() => new PartitionIdentityActor(_cluster, _identityHandoverTimeout))
                    .WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy);
                _partitionIdentityActor = _context.SpawnNamed(partitionActorProps, PartitionIdentityActorName);

                Props? partitionActivatorProps =
                    Props.FromProducer(() => new PartitionPlacementActor(_cluster));
                _partitionPlacementActor = _context.SpawnNamed(partitionActivatorProps, PartitionPlacementActorName);

                //synchronous subscribe to keep accurate

                ulong eventId = 0ul;
                //make sure selector is updated first
                _system.EventStream.Subscribe<ClusterTopology>(e =>
                    {
                        if (e.EventId == eventId)
                        {
                            return;
                        }

                        eventId = e.EventId;

                        Selector.Update(e.Members.ToArray());
                        _context.Send(_partitionIdentityActor, e);
                        _context.Send(_partitionPlacementActor, e);
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
                _context.Stop(_partitionIdentityActor);
                _context.Stop(_partitionPlacementActor);
            }
        }

        public static PID RemotePartitionIdentityActor(string address) =>
            PID.FromAddress(address, PartitionIdentityActorName);

        public static PID RemotePartitionPlacementActor(string address) =>
            PID.FromAddress(address, PartitionPlacementActorName);
    }
}
