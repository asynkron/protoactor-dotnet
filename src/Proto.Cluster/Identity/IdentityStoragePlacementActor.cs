// -----------------------------------------------------------------------
//   <copyright file="Activator.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Timers;

namespace Proto.Cluster.Identity
{
    class IdentityStoragePlacementActor : IActor, IDisposable
    {
        private const int PersistenceRetries = 3;
        private readonly Cluster _cluster;

        private readonly IdentityStorageLookup _identityLookup;
        private readonly ILogger _logger;

        //pid -> the actor that we have created here
        //kind -> the actor kind
        //eventId -> the cluster wide eventId when this actor was created
        private readonly Dictionary<ClusterIdentity, PID> _myActors = new();
        private CancellationTokenSource? _ct;

        public IdentityStoragePlacementActor(Cluster cluster, IdentityStorageLookup identityLookup)
        {
            _cluster = cluster;
            _identityLookup = identityLookup;
            _logger = Log.CreateLogger($"{nameof(IdentityStoragePlacementActor)}-{cluster.LoggerId}");
        }

        public Task ReceiveAsync(IContext context) => context.Message switch
        {
            Started _             => Started(context),
            Stopping _            => Stopping(context),
            Stopped _             => Stopped(context),
            Tick _                => Tick(context),
            Terminated msg        => Terminated(context, msg),
            ActivationRequest msg => ActivationRequest(context, msg),
            _                     => Task.CompletedTask
        };

        public void Dispose() => _ct?.Dispose();

        private Task Started(IContext context)
        {
            _ct = context.Scheduler().SendRepeatedly(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), context.Self!, new Tick());
            return Task.CompletedTask;
        }

        private Task Stopping(IContext context)
        {
            _logger.LogInformation("Stopping placement actor");
            return Task.CompletedTask;
        }

        private Task Stopped(IContext context)
        {
            _logger.LogInformation("Stopped placement actor");
            return Task.CompletedTask;
        }

        private Task Tick(IContext context)
        {
            var count = _myActors.Count;
            _logger.LogDebug("Statistics: Actor Count {ActorCount}", count);
            return Task.CompletedTask;
        }

        private async Task Terminated(IContext context, Terminated msg)
        {
            if (context.System.Shutdown.IsCancellationRequested) return;

            var (identity, pid) = _myActors.FirstOrDefault(kvp => kvp.Value.Equals(msg.Who));
            if (identity != null && pid != null)
            {
                _myActors.Remove(identity);
                _cluster.PidCache.RemoveByVal(identity, pid);

                try
                {
                    await _identityLookup.RemovePidAsync(msg.Who, CancellationToken.None);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to remove {Activation} from storage", pid);
                }
            }


        }

        private Task ActivationRequest(IContext context, ActivationRequest msg)
        {
            var props = _cluster.GetClusterKind(msg.Kind);
            
            try
            {
                if (_myActors.TryGetValue(msg.ClusterIdentity, out var existing))
                {
                    //this identity already exists
                    Respond(existing);
                }
                else
                {
                    //this actor did not exist, lets spawn a new activation

                    //spawn and remember this actor
                    //as this id is unique for this activation (id+counter)
                    //we cannot get ProcessNameAlreadyExists exception here
                    var clusterProps = props.WithClusterInit(_cluster, msg.ClusterIdentity);
                    var pid = context.SpawnPrefix(clusterProps, msg.ClusterIdentity.ToString());

                    //Do not expose the PID externally before we have persisted the activation
                    var completionCallback = new TaskCompletionSource<PID?>();

                    context.ReenterAfter(Task.Run(() => PersistActivation(context, msg, pid)), persistResult =>
                        {
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
                _logger.LogError(e, "Failed to spawn {Kind}/{Identity}", msg.Kind, msg.Identity);
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
                    _logger.LogError("We no longer own the lock {@SpawnLock}", spawnLock);
                    return false;
                }
                catch (Exception e)
                {
                    if (++attempts < PersistenceRetries)
                    {
                        _logger.LogWarning(e, "No entry was updated {@SpawnLock}. Retrying.", spawnLock);
                        await Task.Delay(50);
                    }
                    else
                    {
                        _logger.LogError(e, "Failed to persist activation: {@SpawnLock}", spawnLock);
                        return false;
                    }
                }
            }
        }
    }
}