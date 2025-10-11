// -----------------------------------------------------------------------
// <copyright file="PartitionActivationLifecycle.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Cluster.Identity;
using Proto.Logging;
using Proto.Utils;

namespace Proto.Cluster.Partition;

/// <summary>
/// Manages activation spawning, verification and termination.
/// </summary>
internal sealed class PartitionActivationLifecycle
{
    private static readonly ILogger Logger = Log.CreateLogger<PartitionActivationLifecycle>();

    private readonly Cluster _cluster;
    private readonly Dictionary<ClusterIdentity, PID> _actors;
    private readonly HashSet<ClusterIdentity> _inFlightIdentityChecks;

    public PartitionActivationLifecycle(Cluster cluster, Dictionary<ClusterIdentity, PID> actors,
        HashSet<ClusterIdentity> inFlightIdentityChecks)
    {
        _cluster = cluster;
        _actors = actors;
        _inFlightIdentityChecks = inFlightIdentityChecks;
    }

    public EventStreamSubscription<object> OnStarted(IContext context) =>
        context.System.EventStream.Subscribe<ActivationTerminating>(e => context.Send(context.Self, e));

    public async Task OnStopping(IContext context)
    {
        var pids = _actors.Values;
        await pids.StopMany(context);
    }

    public Task OnActivationTerminating(ActivationTerminating msg)
    {
        if (!_actors.TryGetValue(msg.ClusterIdentity, out var pid))
        {
            Logger.LogWarning("[PartitionPlacementActor] Activation not found: {ActivationTerminating}", msg);

            return Task.CompletedTask;
        }

        if (!pid.Equals(msg.Pid))
        {
            Logger.LogWarning("[PartitionPlacementActor] Activation did not match pid: {ActivationTerminating}, {Pid}", msg,
                pid);

            return Task.CompletedTask;
        }

        _actors.Remove(msg.ClusterIdentity);

        var activationTerminated = new ActivationTerminated
        {
            Pid = pid,
            ClusterIdentity = msg.ClusterIdentity
        };

        _cluster.MemberList.BroadcastEvent(activationTerminated);

        return Task.CompletedTask;
    }

    public async Task OnActivationRequest(IContext context, ActivationRequest msg)
    {
        if (context.System.Metrics.Enabled)
        {
            IdentityMetrics.ActivationRequestReceivedCount.Add(1,
                new KeyValuePair<string, object?>("id", context.System.Id),
                new KeyValuePair<string, object?>("address", context.System.Address),
                new KeyValuePair<string, object?>("clusterkind", msg.Kind));
        }

        if (_actors.TryGetValue(msg.ClusterIdentity, out var existing))
        {
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("[PartitionPlacementActor] Activation already exists: {ClusterIdentity}, {Pid}",
                    msg.ClusterIdentity, existing);
            }

            var response = new ActivationResponse
            {
                Pid = existing,
                TopologyHash = msg.TopologyHash
            };

            context.Respond(response);

            return;
        }

        var clusterKind = _cluster.TryGetClusterKind(msg.Kind);

        if (clusterKind is null)
        {
            Logger.LogError("Failed to spawn {Kind}/{Identity}, kind not found for member", msg.Kind, msg.Identity);
            context.Respond(new ActivationResponse
            {
                Failed = true,
                TopologyHash = msg.TopologyHash
            });

            return;
        }

        if (clusterKind.CanSpawnIdentity is not null)
        {
            await VerifyAndSpawn(msg, context, clusterKind).ConfigureAwait(false);
        }
        else
        {
            Spawn(msg, context, clusterKind);
        }
    }

    private async Task VerifyAndSpawn(ActivationRequest msg, IContext context, ActivatedClusterKind clusterKind)
    {
        var clusterIdentity = msg.ClusterIdentity;

        if (_inFlightIdentityChecks.Contains(clusterIdentity))
        {
            Logger.LogError("[PartitionPlacementActor] Duplicate activation requests for {ClusterIdentity}", clusterIdentity);

            context.Respond(new ActivationResponse
            {
                Failed = true,
                TopologyHash = msg.TopologyHash
            });

            return;
        }

        var canSpawn = clusterKind.CanSpawnIdentity!(msg.Identity,
            CancellationTokens.FromSeconds(_cluster.Config.ActorSpawnVerificationTimeout));

        if (canSpawn.IsCompleted)
        {
            var canSpawnIdentity = await canSpawn.AsTask().ConfigureAwait(false);
            OnSpawnDecided(msg, context, clusterKind, canSpawnIdentity);

            return;
        }

        _inFlightIdentityChecks.Add(clusterIdentity);

        context.ReenterAfter(canSpawn.AsTask(), async task =>
            {
                _inFlightIdentityChecks.Remove(clusterIdentity);

                if (task.IsCompletedSuccessfully)
                {
                    var canSpawnIdentity = await task.ConfigureAwait(false);
                    OnSpawnDecided(msg, context, clusterKind, canSpawnIdentity);
                }
                else
                {
                    Logger.LogError("[PartitionPlacementActor] Error when checking {ClusterIdentity}", clusterIdentity);

                    context.Respond(new ActivationResponse
                        {
                            Failed = true,
                            TopologyHash = msg.TopologyHash
                        }
                    );
                }
            }
        );
    }

    private void Spawn(ActivationRequest msg, IContext context, ActivatedClusterKind clusterKind)
    {
        try
        {
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("[PartitionPlacementActor] Spawning {Kind}/{Identity}", msg.Kind, msg.Identity);
            }
            var pid = context.Spawn(clusterKind.Props, ctx => ctx.Set(msg.ClusterIdentity));
            _actors.Add(msg.ClusterIdentity, pid);

            context.Respond(new ActivationResponse
                {
                    Pid = pid,
                    TopologyHash = msg.TopologyHash
                }
            );
        }
        catch (Exception e)
        {
            e.CheckFailFast();
            Logger.LogError(e, "[PartitionPlacementActor] Failed to spawn {Kind}/{Identity}", msg.Kind, msg.Identity);
            context.Respond(new ActivationResponse
            {
                Failed = true,
                TopologyHash = msg.TopologyHash
            });
        }
    }

    private void OnSpawnDecided(ActivationRequest msg, IContext context, ActivatedClusterKind clusterKind,
        bool canSpawnIdentity)
    {
        if (canSpawnIdentity)
        {
            Spawn(msg, context, clusterKind);
        }
        else
        {
            context.Respond(new ActivationResponse
                {
                    Failed = true,
                    InvalidIdentity = true,
                    TopologyHash = msg.TopologyHash
                }
            );
        }
    }
}
