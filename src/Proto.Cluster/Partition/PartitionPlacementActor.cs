// -----------------------------------------------------------------------
// <copyright file="PartitionPlacementActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Proto.Cluster.Identity;
using Proto.Utils;

namespace Proto.Cluster.Partition;

internal class PartitionPlacementActor : IActor, IDisposable
{
    private readonly Dictionary<ClusterIdentity, PID> _actors = new();
    private readonly Cluster _cluster;
    private readonly PartitionConfig _config;
    private readonly HashSet<ClusterIdentity> _inFlightIdentityChecks = new();

    // Handles identity handovers and rebalance publishing
    private readonly PartitionPlacementRebalance _rebalance;
    // Manages spawning and termination of activations
    private readonly PartitionActivationLifecycle _activationLifecycle;

    private EventStreamSubscription<object>? _subscription;

    public PartitionPlacementActor(Cluster cluster, PartitionConfig config)
    {
        _cluster = cluster;
        _config = config;
        _rebalance = new PartitionPlacementRebalance(cluster, config, _actors);
        _activationLifecycle = new PartitionActivationLifecycle(cluster, _actors, _inFlightIdentityChecks);
    }

    public Task ReceiveAsync(IContext context) =>
        context.Message switch
        {
            Started                     => Start(context),
            Stopping                    => _activationLifecycle.OnStopping(context),
            ActivationTerminating msg   => _activationLifecycle.OnActivationTerminating(msg),
            IdentityHandoverRequest msg => _rebalance.OnIdentityHandoverRequest(context, msg),
            ClusterTopology msg         => _rebalance.OnClusterTopology(context, msg),
            ActivationRequest msg       => _activationLifecycle.OnActivationRequest(context, msg),
            _                           => Task.CompletedTask
        };

    private Task Start(IContext context)
    {
        _subscription = _activationLifecycle.OnStarted(context);
        return Task.CompletedTask;
    }

    public void Dispose() => _subscription?.Unsubscribe();
}
