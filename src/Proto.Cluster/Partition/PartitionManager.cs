// -----------------------------------------------------------------------
// <copyright file="PartitionManager.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Linq;
using System.Threading.Tasks;

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
        private PID _partitionPlacementActor = null!;
        private PID _partitionIdentityActor = null!;
        private readonly TimeSpan _identityHandoverTimeout;

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
                var eventId = 0ul;
                //make sure selector is updated first
                _system.EventStream.Subscribe<ClusterTopology>(e => {
                        if (e.TopologyHash == eventId) return;

                        eventId = e.TopologyHash;
                        Selector.Update(e.Members.ToArray());
                    }
                );
            }
            else
            {
                var partitionActorProps = Props
                    .FromProducer(() => new PartitionIdentityActor(_cluster, _identityHandoverTimeout))
                    .WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy);
                _partitionIdentityActor = _context.SpawnNamed(partitionActorProps, PartitionIdentityActorName);

                var partitionActivatorProps =
                    Props.FromProducer(() => new PartitionPlacementActor(_cluster));
                _partitionPlacementActor = _context.SpawnNamed(partitionActivatorProps, PartitionPlacementActorName);

                //synchronous subscribe to keep accurate

                var topologyHash = 0ul;
                //make sure selector is updated first
                _system.EventStream.Subscribe<ClusterTopology>(e => {
                        if (e.TopologyHash == topologyHash) return;

                        topologyHash = e.TopologyHash;

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