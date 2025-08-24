// -----------------------------------------------------------------------
// <copyright file="PartitionManager.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading.Tasks;

namespace Proto.Cluster.PartitionActivator;

//helper to interact with partition actors on this and other members
public class PartitionActivatorManager
{
    private const string PartitionActivatorActorName = "$partition-activator";
    private readonly Cluster _cluster;

    private readonly IRootContext _context;
    private readonly bool _isClient;
    private readonly ActorSystem _system;
    private readonly Func<Props, Props>? _configureProps;
    private PID _partitionActivatorActor = null!;

    internal PartitionActivatorManager(Cluster cluster, bool isClient, Func<Props, Props>? configureProps = null)
    {
        _cluster = cluster;
        _system = cluster.System;
        _context = _system.Root;
        _isClient = isClient;
        _configureProps = configureProps;
    }

    internal PartitionActivatorSelector Selector { get; } = new();

    public void Setup()
    {
        if (_isClient)
        {
            var topologyHash = 0ul;

            //make sure selector is updated first
            _system.EventStream.Subscribe<ClusterTopology>(e =>
                {
                    if (e.TopologyHash == topologyHash)
                    {
                        return;
                    }

                    topologyHash = e.TopologyHash;
                    Selector.Update(e.Members.ToArray());
                }
            );
        }
        else
        {
            var partitionActivatorProps =
                Props.FromProducer(() => new PartitionActivatorActor(_cluster, this));

            if (_configureProps is not null)
            {
                partitionActivatorProps = _configureProps(partitionActivatorProps);
            }

            _partitionActivatorActor = _context.SpawnNamedSystem(partitionActivatorProps, PartitionActivatorActorName);

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

                    Selector.Update(e.Members.ToArray());
                    _context.Send(_partitionActivatorActor, e);
                }
            );
        }
    }

    public async Task ShutdownAsync()
    {
        if (!_isClient)
        {
            await _context.StopAsync(_partitionActivatorActor).ConfigureAwait(false);
        }
    }

    public static PID RemotePartitionActivatorActor(string address) =>
        PID.FromAddress(address, PartitionActivatorActorName);
}