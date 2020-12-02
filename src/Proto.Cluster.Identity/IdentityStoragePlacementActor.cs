// -----------------------------------------------------------------------
// <copyright file="IdentityStoragePlacementActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster.Identity
{
    internal class IdentityStoragePlacementActor : IActor
    {
        private static readonly Random Jitter = new();
        private readonly Cluster _cluster;

        private readonly IdentityStorageLookup _identityLookup;
        private readonly ILogger _logger;
        private readonly Dictionary<ClusterIdentity, PID> _myActors = new();

        //pid -> the actor that we have created here
        //kind -> the actor kind
        //eventId -> the cluster wide eventId when this actor was created
        private readonly Dictionary<ClusterIdentity, Task<PID?>> _pendingActivations = new();

        public IdentityStoragePlacementActor(Cluster cluster, IdentityStorageLookup identityLookup)
        {
            _cluster = cluster;
            _identityLookup = identityLookup;
            _logger = Log.CreateLogger($"{nameof(IdentityStoragePlacementActor)}-{cluster.LoggerId}");
        }

        public Task ReceiveAsync(IContext context)
        {
            return context.Message switch
                   {
                       Started _             => Started(context),
                       ReceiveTimeout _      => ReceiveTimeout(context),
                       Terminated msg        => Terminated(msg),
                       ActivationRequest msg => ActivationRequest(context, msg),
                       _                     => Task.CompletedTask
                   };
        }

        private static Task Started(IContext context)
        {
            context.SetReceiveTimeout(TimeSpan.FromSeconds(5));
            return Task.CompletedTask;
        }

        private Task ReceiveTimeout(IContext context)
        {
            context.SetReceiveTimeout(TimeSpan.FromSeconds(5));
            var count = _myActors.Count;
            _logger.LogDebug("Statistics: Actor Count {ActorCount}", count);
            return Task.CompletedTask;
        }

        private async Task Terminated(Terminated msg)
        {
            //TODO: if this turns out to be perf intensive, lets look at optimizations for reverse lookups
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
                    _logger.LogWarning(e, "Failed to remove pid from storage");
                }
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
                else if (_pendingActivations.TryGetValue(msg.ClusterIdentity, out var task))
                {
                    //Already pending, wait for result in reentrant context
                    context.ReenterAfter(task, completedTask =>
                        {
                            try
                            {
                                Respond(completedTask.Result);
                                return Task.CompletedTask;
                            }
                            catch (Exception e)
                            {
                                _logger.LogError(e, "Failed to respond to pending: {Kind}/{Identity}", msg.Kind, msg.Identity);
                                throw;
                            }
                        }
                    );
                }
                else
                {
                    //this actor did not exist, lets spawn a new activation

                    //spawn and remember this actor
                    //as this id is unique for this activation (id+counter)
                    //we cannot get ProcessNameAlreadyExists exception here
                    var props = _cluster.GetClusterKind(msg.Kind);
                    var clusterProps = props.WithClusterInit(_cluster, msg.ClusterIdentity);
                    var pid = context.SpawnPrefix(clusterProps, msg.ClusterIdentity.ToShortString());

                    //Do not expose the PID externally before we have persisted the activation
                    var completionCallback = new TaskCompletionSource<PID?>();
                    _pendingActivations.Add(msg.ClusterIdentity, completionCallback.Task);

                    context.ReenterAfter(Task.Run(() => PersistActivation(context, msg, pid)), persistResult =>
                        {
                            var wasPersistedCorrectly = persistResult.Result;
                            _pendingActivations.Remove(msg.ClusterIdentity);

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
            while (attempts < 3)
            {
                try
                {
                    await _identityLookup.Storage.StoreActivation(_cluster.Id.ToString(), spawnLock, pid,
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
                    _logger.LogError(e, "No entry was updated {@SpawnLock}", spawnLock);
                    attempts++;
                }

                await Task.Delay(50 + Jitter.Next(100));
            }

            return false;
        }
    }
}