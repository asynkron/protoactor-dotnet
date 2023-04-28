// -----------------------------------------------------------------------
// <copyright file="PartitionManager.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Linq;
using System.Threading.Tasks;

namespace Proto.Cluster.Partition;

//helper to interact with partition actors on this and other members
internal class PartitionManager
{
    private const string PartitionIdentityActorName = "$partition-identity";
    private const string PartitionPlacementActorName = "$partition-activator";
    private readonly Cluster _cluster;
    private readonly PartitionConfig _config;
    private readonly IRootContext _context;
    private readonly bool _isClient;
    private readonly ActorSystem _system;
    private PID _partitionIdentityActor = null!;
    private PID _partitionPlacementActor = null!;

    internal PartitionManager(Cluster cluster, bool isClient, PartitionConfig config)
    {
        _cluster = cluster;
        _system = cluster.System;
        _context = _system.Root;
        _isClient = isClient;
        _config = config;
    }

    internal PartitionMemberSelector Selector { get; } = new();

    public void Setup()
    {
        if (_isClient)
        {
            var eventId = 0ul;

            //make sure selector is updated first
            _system.EventStream.Subscribe<ClusterTopology>(e =>
                {
                    if (e.TopologyHash == eventId)
                    {
                        return;
                    }

                    eventId = e.TopologyHash;
                    Selector.Update(e.Members.ToArray(), e.TopologyHash);
                }
            );
        }
        else
        {
            var partitionActorProps = Props
                .FromProducer(() => new PartitionIdentityActor(_cluster, _config));

            _partitionIdentityActor = _context.SpawnNamedSystem(partitionActorProps, PartitionIdentityActorName);

            var partitionActivatorProps = Props.FromProducer(() => new PartitionPlacementActor(_cluster, _config));
            _partitionPlacementActor = _context.SpawnNamedSystem(partitionActivatorProps, PartitionPlacementActorName);

            //synchronous subscribe to keep accurate
            var topologyHash = 0ul;

            //make sure selector is updated first
            _system.EventStream.Subscribe<ClusterTopology>(e =>
                {
                    if (e.TopologyHash == topologyHash)
                    {
                        return;
                    }

                    topologyHash = e.TopologyHash;

                    Selector.Update(e.Members.ToArray(), e.TopologyHash);

                    _context.Send(_partitionIdentityActor, e);
                    _context.Send(_partitionPlacementActor, e);
                }
            );
        }
    }

    public async Task ShutdownAsync()
    {
        if (_isClient)
        {
        }
        else
        {
            await Task.WhenAll(
                _context.StopAsync(_partitionPlacementActor),
                _context.StopAsync(_partitionIdentityActor)
            ).ConfigureAwait(false);
        }
    }

    public static PID RemotePartitionIdentityActor(string address) =>
        PID.FromAddress(address, PartitionIdentityActorName);

    public static PID RemotePartitionPlacementActor(string address) =>
        PID.FromAddress(address, PartitionPlacementActorName);
}