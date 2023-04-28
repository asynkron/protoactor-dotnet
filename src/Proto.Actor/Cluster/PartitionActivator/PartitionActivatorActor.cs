// -----------------------------------------------------------------------
// <copyright file="PartitionActivatorActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Utils;

namespace Proto.Cluster.PartitionActivator;

public class PartitionActivatorActor : IActor
{
    private static readonly ILogger Logger = Log.CreateLogger<PartitionActivatorActor>();
    private readonly Dictionary<ClusterIdentity, PID> _actors = new();
    private readonly Cluster _cluster;
    private readonly HashSet<ClusterIdentity> _inFlightIdentityChecks = new();
    private readonly PartitionActivatorManager _manager;
    private readonly string _myAddress;

    private readonly ShouldThrottle _wrongPartitionLogThrottle = Throttle.Create(1, TimeSpan.FromSeconds(1),
        wrongNodeCount =>
        {
            if (wrongNodeCount > 1)
            {
                Logger.LogWarning("[PartitionActivator] Forwarded {SpawnCount} attempts to spawn on wrong node",
                    wrongNodeCount);
            }
        }
    );

    private ulong _topologyHash;

    public PartitionActivatorActor(Cluster cluster, PartitionActivatorManager manager)
    {
        _cluster = cluster;
        _manager = manager;
        _myAddress = cluster.System.Address;
    }

    public Task ReceiveAsync(IContext context) =>
        context.Message switch
        {
            Started                   => OnStarted(context),
            ActivationRequest msg     => OnActivationRequest(msg, context),
            ActivationTerminated msg  => OnActivationTerminated(msg),
            ActivationTerminating msg => OnActivationTerminating(msg),
            ClusterTopology msg       => OnClusterTopology(msg, context),
            _                         => Task.CompletedTask
        };

    private Task OnStarted(IContext context)
    {
        var self = context.Self;
        _cluster.System.EventStream.Subscribe<ActivationTerminated>(context.System.Root, context.Self);
        _cluster.System.EventStream.Subscribe<ActivationTerminating>(context.System.Root, context.Self);

        return Task.CompletedTask;
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
        Logger.LogWarning("[PartitionActivator] ClusterTopology - Stopping {ActorCount} actors", toRemove.Count);
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
        Logger.LogWarning("[PartitionActivator] ClusterTopology - Stopped {ActorCount} actors", toRemove.Count);

        // Remove all cached PIDs from PidCache that now points to
        // an address where the ClusterIdentity doesn't belong.
        _cluster.PidCache.RemoveByPredicate(cache =>
            _manager.Selector.GetOwnerAddress(cache.Key) != cache.Value.Address
        );
    }

    private Task OnActivationTerminated(ActivationTerminated msg)
    {
        _cluster.PidCache.RemoveByVal(msg.ClusterIdentity, msg.Pid);

        // we get this via broadcast to all nodes, remove if we have it, or ignore
        if (Logger.IsEnabled(LogLevel.Trace))
        {
            Logger.LogTrace("[PartitionActivator] Terminated {Pid}", msg.Pid);
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
            Logger.LogTrace("[PartitionActivator] Terminating {Pid}", msg.Pid);
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
        //who owns this?
        var ownerAddress = _manager.Selector.GetOwnerAddress(msg.ClusterIdentity);

        //is it not me?
        if (ownerAddress != _myAddress)
        {
            //get the owner
            var ownerPid = PartitionActivatorManager.RemotePartitionActivatorActor(ownerAddress);

            if (_wrongPartitionLogThrottle().IsOpen())
            {
                Logger.LogWarning("[PartitionActivator] Tried to spawn on wrong node, forwarding");
            }

            context.Forward(ownerPid);

            return Task.CompletedTask;
        }

        if (_actors.TryGetValue(msg.ClusterIdentity, out var existing))
        {
            context.Respond(new ActivationResponse
                {
                    Pid = existing,
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
            Logger.LogError("[PartitionActivator] Duplicate activation requests for {ClusterIdentity}",
                clusterIdentity);

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
                    Logger.LogError("[PartitionActivator] Error when checking {ClusterIdentity}", clusterIdentity);

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
            Logger.LogError(e, "[PartitionActivator] Failed to spawn {Kind}/{Identity}", msg.Kind, msg.Identity);
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