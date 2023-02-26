// -----------------------------------------------------------------------
// <copyright file="SingleNodeActivatorActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster.SingleNode;

internal class SingleNodeActivatorActor : IActor
{
    private static readonly ILogger Logger = Log.CreateLogger<SingleNodeActivatorActor>();
    private readonly Dictionary<ClusterIdentity, PID> _actors = new();

    private readonly Cluster _cluster;
    private readonly HashSet<ClusterIdentity> _inFlightIdentityChecks = new();

    public SingleNodeActivatorActor(Cluster cluster)
    {
        _cluster = cluster;
    }

    public Task ReceiveAsync(IContext context) =>
        context.Message switch
        {
            Started                   => OnStarted(context),
            Stopping                  => OnStopping(context),
            ActivationRequest msg     => OnActivationRequest(msg, context),
            ActivationTerminated msg  => OnActivationTerminated(msg),
            ActivationTerminating msg => OnActivationTerminating(msg),
            _                         => Task.CompletedTask
        };

    private Task OnStarted(IContext context)
    {
        var self = context.Self;
        _cluster.System.EventStream.Subscribe<ActivationTerminated>(context.System.Root, self);
        _cluster.System.EventStream.Subscribe<ActivationTerminating>(context.System.Root, self);

        return Task.CompletedTask;
    }

    private async Task OnStopping(IContext context)
    {
        await StopActors(context).ConfigureAwait(false);

        _cluster.PidCache.RemoveByPredicate(kv =>
            kv.Value.Address.Equals(context.System.Address, StringComparison.Ordinal));
    }

    private async Task StopActors(IContext context)
    {
        var stopping = new List<Task>();

        var clusterIdentities = _actors.Keys.ToList();

        foreach (var ci in clusterIdentities)
        {
            var pid = _actors[ci];
            var stoppingTask = context.PoisonAsync(pid);
            stopping.Add(stoppingTask);
            _actors.Remove(ci);
        }

        //await graceful shutdown of all actors
        await Task.WhenAll(stopping).ConfigureAwait(false);
        Logger.LogInformation("[SingleNode] - Stopped {ActorCount} actors", clusterIdentities.Count);
    }

    private Task OnActivationTerminated(ActivationTerminated msg)
    {
        _cluster.PidCache.RemoveByVal(msg.ClusterIdentity, msg.Pid);

        if (Logger.IsEnabled(LogLevel.Trace))
        {
            Logger.LogTrace("[SingleNode] Terminated {Pid}", msg.Pid);
        }

        return Task.CompletedTask;
    }

    private Task OnActivationTerminating(ActivationTerminating msg)
    {
        // ActivationTerminating is sent to the local EventStream when a
        // local cluster actor stops.

        if (!_actors.ContainsKey(msg.ClusterIdentity))
        {
            return Task.CompletedTask;
        }

        if (Logger.IsEnabled(LogLevel.Trace))
        {
            Logger.LogTrace("[SingleNode] Terminating {Pid}", msg.Pid);
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

    private Task OnActivationRequest(ActivationRequest msg, IContext context)
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
                VerifyAndSpawn(msg, context, clusterKind);
            }
            else
            {
                Spawn(msg, context, clusterKind);
            }
        }

        return Task.CompletedTask;
    }

    private void VerifyAndSpawn(ActivationRequest msg, IContext context, ActivatedClusterKind clusterKind)
    {
        var clusterIdentity = msg.ClusterIdentity;

        if (_inFlightIdentityChecks.Contains(clusterIdentity))
        {
            Logger.LogError("[SingleNode] Duplicate activation requests for {ClusterIdentity}", clusterIdentity);

            context.Respond(new ActivationResponse
                {
                    Failed = true
                }
            );
        }

        var canSpawn = clusterKind.CanSpawnIdentity!(msg.Identity,
            CancellationTokens.FromSeconds(_cluster.Config.ActorSpawnVerificationTimeout));

        if (canSpawn.IsCompleted)
        {
            OnSpawnDecided(msg, context, clusterKind, canSpawn.Result);

            return;
        }

        _inFlightIdentityChecks.Add(clusterIdentity);

        context.ReenterAfter(canSpawn.AsTask(), task =>
            {
                _inFlightIdentityChecks.Remove(clusterIdentity);

                if (task.IsCompletedSuccessfully)
                {
                    OnSpawnDecided(msg, context, clusterKind, task.Result);
                }
                else
                {
                    Logger.LogError("[SingleNode] Error when checking {ClusterIdentity}", clusterIdentity);

                    context.Respond(new ActivationResponse
                        {
                            Failed = true
                        }
                    );
                }

                return Task.CompletedTask;
            }
        );
    }

    private void Spawn(ActivationRequest msg, IContext context, ActivatedClusterKind clusterKind)
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
            Logger.LogError(e, "[SingleNode] Failed to spawn {Kind}/{Identity}", msg.Kind, msg.Identity);
            context.Respond(new ActivationResponse { Failed = true });
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
                    InvalidIdentity = true
                }
            );
        }
    }
}