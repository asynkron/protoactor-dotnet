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

namespace Proto.Cluster.Identity
{
    class IdentityStoragePlacementActor : IActor
    {
        private const int PersistenceRetries = 3;
        private readonly Cluster _cluster;

        private readonly IdentityStorageLookup _identityLookup;
        private static readonly ILogger Logger = Log.CreateLogger<IdentityStoragePlacementActor>();

        //pid -> the actor that we have created here
        //kind -> the actor kind
        //eventId -> the cluster wide eventId when this actor was created
        private readonly Dictionary<ClusterIdentity, PID> _myActors = new();
        private EventStreamSubscription<object>? _subscription;

        public IdentityStoragePlacementActor(Cluster cluster, IdentityStorageLookup identityLookup)
        {
            _cluster = cluster;
            _identityLookup = identityLookup;
        }

        public Task ReceiveAsync(IContext context) => context.Message switch
        {
            Started                   => OnStarted(context),
            Stopping _                => Stopping(),
            Stopped _                 => Stopped(),
            ActivationTerminating msg => Terminated(context, msg),
            ActivationRequest msg     => ActivationRequest(context, msg),
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
            Logger.LogInformation("Stopped placement actor");
            return Task.CompletedTask;
        }

        private async Task Terminated(IContext context, ActivationTerminating msg)
        {
            if (context.System.Shutdown.IsCancellationRequested) return;
            
            if (!_myActors.TryGetValue(msg.ClusterIdentity, out var pid))
            {
                Logger.LogWarning("Activation not found: {ActivationTerminating}", msg);
                return;
            }

            if (!pid.Equals(msg.Pid))
            {
                Logger.LogWarning("Activation did not match pid: {ActivationTerminating}, {Pid}", msg, pid);
                return;
            }


            _myActors.Remove(msg.ClusterIdentity);
            _cluster.PidCache.RemoveByVal(msg.ClusterIdentity, pid);

            try
            {
                await _identityLookup.RemovePidAsync(msg.ClusterIdentity, pid, CancellationToken.None);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Failed to remove {Activation} from storage", pid);
            }
        }

        private Task ActivationRequest(IContext context, ActivationRequest msg)
        {
            try
            {
                if (_myActors.TryGetValue(msg.ClusterIdentity, out var existing))
                {
                    //this identity already exists
                    Respond(existing);
                }
                else
                {
                    var clusterKind = _cluster.GetClusterKind(msg.Kind);
                    //this actor did not exist, lets spawn a new activation

                    //spawn and remember this actor
                    //as this id is unique for this activation (id+counter)
                    //we cannot get ProcessNameAlreadyExists exception here
                    var clusterProps = clusterKind.Props.WithClusterIdentity(msg.ClusterIdentity);

                    var sw = Stopwatch.StartNew();
                    var pid = context.SpawnPrefix(clusterProps, msg.ClusterIdentity.ToString());
                    sw.Stop();

                    if (_cluster.System.Metrics.Enabled)
                    {
                        ClusterMetrics.ClusterActorSpawnDuration
                            .Record(sw.Elapsed.TotalSeconds,
                                new("id", _cluster.System.Id), new("address", _cluster.System.Address), new("clusterkind", msg.Kind)
                            );
                    }

                    //Do not expose the PID externally before we have persisted the activation
                    var completionCallback = new TaskCompletionSource<PID?>();

                    context.ReenterAfter(Task.Run(() => PersistActivation(context, msg, pid)), persistResult => {
                            var wasPersistedCorrectly = persistResult.Result;

                            if (wasPersistedCorrectly)
                            {
                                _myActors[msg.ClusterIdentity] = pid;
                                _cluster.PidCache.TryAdd(msg.ClusterIdentity, pid);
                                completionCallback.SetResult(pid);
                                Respond(pid);
                            }
                            else // Not stored, kill it and retry later?
                            {
                                Respond(null);
                                context.Poison(pid);
                                completionCallback.SetResult(null);
                            }

                            return Task.CompletedTask;
                        }
                    );
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Failed to spawn {Kind}/{Identity}", msg.Kind, msg.Identity);
                Respond(null);
            }

            return Task.CompletedTask;

            void Respond(PID? result)
            {
                var response = new ActivationResponse
                {
                    Pid = result
                };
                context.Respond(response);
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
                    );
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
                        await Task.Delay(50);
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
}