// -----------------------------------------------------------------------
//   <copyright file="Activator.cs" company="Asynkron AB">
//       Copyright (C) 2015-2022 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Cluster.Metrics;

namespace Proto.Cluster.Identity;

internal class IdentityStoragePlacementActor : IActor
{
    private const int PersistenceRetries = 3;
    private static readonly ILogger Logger = Log.CreateLogger<IdentityStoragePlacementActor>();

    //pid -> the actor that we have created here
    //kind -> the actor kind
    //eventId -> the cluster wide eventId when this actor was created
    private readonly Dictionary<ClusterIdentity, PID> _actors = new();
    private readonly Cluster _cluster;

    private readonly IdentityStorageLookup _identityLookup;
    private readonly HashSet<ClusterIdentity> _inFlightIdentityChecks = new();
    private EventStreamSubscription<object>? _subscription;

    public IdentityStoragePlacementActor(Cluster cluster, IdentityStorageLookup identityLookup)
    {
        _cluster = cluster;
        _identityLookup = identityLookup;
    }

    public Task ReceiveAsync(IContext context) =>
        context.Message switch
        {
            Started                   => OnStarted(context),
            Stopping _                => Stopping(),
            Stopped _                 => Stopped(),
            ActivationTerminating msg => OnActivationTerminating(context, msg),
            ActivationRequest msg     => OnActivationRequest(context, msg),
            _                         => Task.CompletedTask
        };

    private Task OnStarted(IContext context)
    {
        _subscription = context.System.EventStream.Subscribe<ActivationTerminating>(e => context.Send(context.Self, e));

        return Task.CompletedTask;
    }

    private Task Stopping()
    {
        Logger.LogInformation("Stopping placement actor");
        _subscription?.Unsubscribe();

        return Task.CompletedTask;
    }

    private Task Stopped()
    {
        Logger.LogDebug("Stopped placement actor");

        return Task.CompletedTask;
    }

    private async Task OnActivationTerminating(IContext context, ActivationTerminating msg)
    {
        if (context.System.Shutdown.IsCancellationRequested)
        {
            return;
        }

        if (!_actors.TryGetValue(msg.ClusterIdentity, out var pid))
        {
            Logger.LogWarning("Activation not found: {ActivationTerminating}", msg);

            return;
        }

        if (!pid.Equals(msg.Pid))
        {
            Logger.LogWarning("Activation did not match pid: {ActivationTerminating}, {Pid}", msg, pid);

            return;
        }

        _actors.Remove(msg.ClusterIdentity);
        _cluster.PidCache.RemoveByVal(msg.ClusterIdentity, pid);

        try
        {
            await _identityLookup.RemovePidAsync(msg.ClusterIdentity, pid, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Failed to remove {Activation} from storage", pid);
        }
    }

    private Task OnActivationRequest(IContext context, ActivationRequest msg)
    {
        if (_actors.TryGetValue(msg.ClusterIdentity, out var existing))
        {
            //this identity already exists
            context.Respond(new ActivationResponse { Pid = existing });

            return Task.CompletedTask;
        }

        var clusterKind = _cluster.TryGetClusterKind(msg.Kind);

        if (clusterKind is null)
        {
            Logger.LogError("Failed to spawn {Kind}/{Identity}, kind not found for member", msg.Kind, msg.Identity);
            context.Respond(new ActivationResponse { Failed = true });

            return Task.CompletedTask;
        }

        if (clusterKind.CanSpawnIdentity is not null)
        {
            // Needs to check if the identity is allowed to spawn
            VerifyAndSpawn(msg, context, clusterKind);
        }
        else
        {
            Spawn(msg, context, clusterKind);
        }

        return Task.CompletedTask;
    }

    private void VerifyAndSpawn(ActivationRequest msg, IContext context, ActivatedClusterKind clusterKind)
    {
        var clusterIdentity = msg.ClusterIdentity;

        if (_inFlightIdentityChecks.Contains(clusterIdentity))
        {
            Logger.LogError("[PartitionIdentity] Duplicate activation requests for {ClusterIdentity}", clusterIdentity);

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
                    Logger.LogError("[PartitionIdentity] Error when checking {ClusterIdentity}", clusterIdentity);

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

    private void Spawn(ActivationRequest msg, IContext context, ActivatedClusterKind clusterKind)
    {
        try
        {
            var sw = Stopwatch.StartNew();

            var pid = context.SpawnPrefix(clusterKind.Props, msg.ClusterIdentity.Identity,
                ctx => ctx.Set(msg.ClusterIdentity));

            sw.Stop();

            if (_cluster.System.Metrics.Enabled)
            {
                ClusterMetrics.ClusterActorSpawnDuration
                    .Record(sw.Elapsed.TotalSeconds,
                        new KeyValuePair<string, object?>("id", _cluster.System.Id),
                        new KeyValuePair<string, object?>("address", _cluster.System.Address),
                        new KeyValuePair<string, object?>("clusterkind", msg.Kind)
                    );
            }

            //Do not expose the PID externally before we have persisted the activation
            var completionCallback = new TaskCompletionSource<PID?>();

            context.ReenterAfter(Task.Run(() => PersistActivation(context, msg, pid)), persistResult =>
                {
                    var wasPersistedCorrectly = persistResult.Result;

                    if (wasPersistedCorrectly)
                    {
                        _actors[msg.ClusterIdentity] = pid;
                        _cluster.PidCache.TryAdd(msg.ClusterIdentity, pid);
                        completionCallback.SetResult(pid);
                        context.Respond(new ActivationResponse { Pid = pid });
                    }
                    else // Not stored, kill it and retry later?
                    {
                        context.Respond(new ActivationResponse { Failed = true });
                        context.Poison(pid);
                        completionCallback.SetResult(null);
                    }

                    return Task.CompletedTask;
                }
            );
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Failed to spawn {Kind}/{Identity}", msg.Kind, msg.Identity);
            context.Respond(new ActivationResponse { Failed = true });
        }
    }

    private async Task<bool> PersistActivation(IContext context, ActivationRequest msg, PID pid)
    {
        var attempts = 0;
        var spawnLock = new SpawnLock(msg.RequestId, msg.ClusterIdentity);

        while (true)
        {
            try
            {
                await _identityLookup.Storage.StoreActivation(_cluster.System.Id, spawnLock, pid,
                    context.CancellationToken
                ).ConfigureAwait(false);

                return true;
            }
            catch (LockNotFoundException)
            {
                Logger.LogWarning("We no longer own the lock {@SpawnLock}", spawnLock);

                return false;
            }
            catch (Exception e)
            {
                if (++attempts < PersistenceRetries)
                {
                    Logger.LogWarning(e, "No entry was updated {@SpawnLock}. Retrying", spawnLock);
                    await Task.Delay(50).ConfigureAwait(false);
                }
                else
                {
                    Logger.LogError(e, "Failed to persist activation: {@SpawnLock}", spawnLock);

                    return false;
                }
            }
        }
    }
}