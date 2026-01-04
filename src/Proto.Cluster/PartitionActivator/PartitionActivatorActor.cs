// -----------------------------------------------------------------------
// <copyright file="PartitionActivatorActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Utils;
using Proto.Cluster.Identity;

namespace Proto.Cluster.PartitionActivator;

public class PartitionActivatorActor : BaseActivatorActor
{
    private static readonly ILogger _logger = Log.CreateLogger<PartitionActivatorActor>();
    private readonly PartitionActivatorManager _manager;
    private readonly string _myAddress;

    private readonly ShouldThrottle _wrongPartitionLogThrottle = Throttle.Create(1, TimeSpan.FromSeconds(1),
        wrongNodeCount =>
        {
            if (wrongNodeCount > 1)
            {
                _logger.LogWarning("[PartitionActivator] Forwarded {SpawnCount} attempts to spawn on wrong node",
                    wrongNodeCount);
            }
        }
    );

    private ulong _topologyHash;

    public PartitionActivatorActor(Cluster cluster, PartitionActivatorManager manager) : base(cluster)
    {
        _manager = manager;
        _myAddress = cluster.System.Address;
    }

    protected override ILogger Logger => _logger;
    protected override string LogPrefix => "[PartitionActivator]";

    public override Task ReceiveAsync(IContext context) =>
        context.Message switch
        {
            Started                   => OnStarted(context),
            Stopping                  => OnStopping(context),
            ActivationRequest msg     => OnActivationRequest(msg, context),
            ActivationTerminated msg  => OnActivationTerminated(msg),
            ActivationTerminating msg => OnActivationTerminating(msg),
            ClusterTopology msg       => OnClusterTopology(msg, context),
            _                         => Task.CompletedTask
        };

    private async Task OnStopping(IContext context)
    {
        var pids = _actors.Values;
        await pids.StopMany(context);
    }

    private async Task OnClusterTopology(ClusterTopology msg, IContext context)
    {
        if (msg.TopologyHash == _topologyHash)
        {
            return;
        }

        _topologyHash = msg.TopologyHash;

        var toRemove = _actors
            .Where(kvp => _manager.Selector.GetOwnerAddress(kvp.Key) != _cluster.System.Address)
            .Select(kvp => kvp.Key)
            .ToList();

        //stop and remove all actors we don't own anymore
        Logger.LogWarning("{LogPrefix} ClusterTopology - Stopping {ActorCount} actors", LogPrefix, toRemove.Count);
        var stopping = new List<Task>();

        foreach (var ci in toRemove)
        {
            var pid = _actors[ci];
            var stoppingTask = context.PoisonAsync(pid);
            stopping.Add(stoppingTask);
            _actors.Remove(ci);
        }

        //await graceful shutdown of all actors we no longer own
        await Task.WhenAll(stopping).ConfigureAwait(false);
        Logger.LogWarning("{LogPrefix} ClusterTopology - Stopped {ActorCount} actors", LogPrefix, toRemove.Count);

        // Remove all cached PIDs from PidCache that now points to
        // an address where the ClusterIdentity doesn't belong.
        _cluster.PidCache.RemoveByPredicate(cache =>
            _manager.Selector.GetOwnerAddress(cache.Key) != cache.Value.Address
        );
    }

    private async Task OnActivationRequest(ActivationRequest msg, IContext context)
    {
        //who owns this?
        var ownerAddress = _manager.Selector.GetOwnerAddress(msg.ClusterIdentity);

        //is it not me?
        if (ownerAddress != _myAddress)
        {
            //get the owner
            var ownerPid = PartitionActivatorManager.RemotePartitionActivatorActor(ownerAddress);

            if (_wrongPartitionLogThrottle().IsOpen())
            {
                Logger.LogWarning("{LogPrefix} Tried to spawn on wrong node, forwarding", LogPrefix);
            }

            if (context.System.Metrics.Enabled)
            {
                IdentityMetrics.ActivationRequestForwardedCount.Add(1,
                    new KeyValuePair<string, object?>("id", context.System.Id),
                    new KeyValuePair<string, object?>("address", context.System.Address),
                    new KeyValuePair<string, object?>("clusterkind", msg.Kind));
            }

            context.Forward(ownerPid);

            return;
        }

        if (context.System.Metrics.Enabled)
        {
            IdentityMetrics.RecordActivationRequestReceived(context.System, msg.Kind);
        }

        await TrySpawnOrVerifyAsync(msg, context).ConfigureAwait(false);
    }
}
