// -----------------------------------------------------------------------
// <copyright file="BaseActivatorActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Cluster.Identity;

namespace Proto.Cluster;

/// <summary>
///     Base class for activator actors that share common spawn and activation handling logic.
/// </summary>
public abstract class BaseActivatorActor : IActor
{
    protected readonly Dictionary<ClusterIdentity, PID> _actors = new();
    protected readonly Cluster _cluster;
    protected readonly HashSet<ClusterIdentity> _inFlightIdentityChecks = new();

    protected BaseActivatorActor(Cluster cluster)
    {
        _cluster = cluster;
    }

    /// <summary>
    ///     Gets the logger for this activator.
    /// </summary>
    protected abstract ILogger Logger { get; }

    /// <summary>
    ///     Gets the log prefix used in log messages (e.g., "[SingleNode]" or "[PartitionActivator]").
    /// </summary>
    protected abstract string LogPrefix { get; }

    public abstract Task ReceiveAsync(IContext context);

    protected virtual Task OnStarted(IContext context)
    {
        _cluster.System.EventStream.Subscribe<ActivationTerminated>(context.System.Root, context.Self);
        _cluster.System.EventStream.Subscribe<ActivationTerminating>(context.System.Root, context.Self);

        return Task.CompletedTask;
    }

    protected Task OnActivationTerminated(ActivationTerminated msg)
    {
        _cluster.PidCache.RemoveByVal(msg.ClusterIdentity, msg.Pid);

        if (Logger.IsEnabled(LogLevel.Trace))
        {
            Logger.LogTrace("{LogPrefix} Terminated {Pid}", LogPrefix, msg.Pid);
        }

        return Task.CompletedTask;
    }

    protected Task OnActivationTerminating(ActivationTerminating msg)
    {
        // ActivationTerminating is sent to the local EventStream when a
        // local cluster actor stops.

        if (!_actors.ContainsKey(msg.ClusterIdentity))
        {
            return Task.CompletedTask;
        }

        if (Logger.IsEnabled(LogLevel.Trace))
        {
            Logger.LogTrace("{LogPrefix} Terminating {Pid}", LogPrefix, msg.Pid);
        }

        _actors.Remove(msg.ClusterIdentity);

        // Broadcast ActivationTerminated to all nodes so that PidCaches gets
        // cleared correctly.
        var activationTerminated = new ActivationTerminated
        {
            Pid = msg.Pid,
            ClusterIdentity = msg.ClusterIdentity
        };

        _cluster.MemberList.BroadcastEvent(activationTerminated);

        return Task.CompletedTask;
    }

    protected Task VerifyAndSpawn(ActivationRequest msg, IContext context, ActivatedClusterKind clusterKind) =>
        SpawnVerificationHelper.VerifyAndSpawn(
            msg, context, clusterKind, _cluster, _inFlightIdentityChecks, Spawn, Logger, LogPrefix);

    protected void Spawn(ActivationRequest msg, IContext context, ActivatedClusterKind clusterKind)
    {
        try
        {
            var pid = context.Spawn(clusterKind.Props, ctx => ctx.Set(msg.ClusterIdentity));
            _actors.Add(msg.ClusterIdentity, pid);

            context.Respond(new ActivationResponse
                {
                    Pid = pid
                }
            );
        }
        catch (Exception e)
        {
            e.CheckFailFast();
            Logger.LogError(e, "{LogPrefix} Failed to spawn {Kind}/{Identity}",
                LogPrefix, msg.Kind, msg.Identity);
            context.Respond(new ActivationResponse { Failed = true });
        }
    }

    protected void OnSpawnDecided(ActivationRequest msg, IContext context, ActivatedClusterKind clusterKind,
        bool canSpawnIdentity) =>
        SpawnVerificationHelper.OnSpawnDecided(msg, context, clusterKind, canSpawnIdentity, Spawn);

    protected async Task TrySpawnOrVerifyAsync(ActivationRequest msg, IContext context)
    {
        if (_actors.TryGetValue(msg.ClusterIdentity, out var existing))
        {
            context.Respond(new ActivationResponse
                {
                    Pid = existing
                }
            );
        }
        else
        {
            var clusterKind = _cluster.GetClusterKind(msg.Kind);

            if (clusterKind.CanSpawnIdentity is not null)
            {
                // Needs to check if the identity is allowed to spawn
                await VerifyAndSpawn(msg, context, clusterKind).ConfigureAwait(false);
            }
            else
            {
                Spawn(msg, context, clusterKind);
            }
        }
    }
}
