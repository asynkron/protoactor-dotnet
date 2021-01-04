// -----------------------------------------------------------------------
// <copyright file="IdentityStorageWorker.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Utils;

namespace Proto.Cluster.Identity
{
    class IdentityStorageWorker : IActor
    {
        private const int MaxSpawnRetries = 3;
        
        private static readonly ConcurrentSet<string> StaleMembers = new();

        private readonly Cluster _cluster;

        private readonly HashSet<ClusterIdentity> _inProgress = new();
        private readonly Dictionary<ClusterIdentity, List<PID>> _waitingRequests = new();
        private readonly ILogger _logger = Log.CreateLogger<IdentityStorageWorker>();
        private readonly IdentityStorageLookup _lookup;
        private readonly MemberList _memberList;

        private readonly ShouldThrottle _shouldThrottle;
        private readonly IIdentityStorage _storage;

        public IdentityStorageWorker(IdentityStorageLookup storageLookup)
        {
            _shouldThrottle = Throttle.Create(
                10,
                TimeSpan.FromSeconds(5),
                i => _logger.LogInformation("Throttled {LogCount} IdentityStorageWorker logs.", i)
            );

            _cluster = storageLookup.Cluster;
            _memberList = storageLookup.MemberList;
            _lookup = storageLookup;
            _storage = storageLookup.Storage;
        }

        public Task ReceiveAsync(IContext context)
        {
            if (context.Message is not GetPid msg) return Task.CompletedTask;

            if (context.Sender == null)
            {
                _logger.LogCritical("No sender in GetPid request");
                return Task.CompletedTask;
            }

            var clusterIdentity = msg.ClusterIdentity;

            if (_cluster.PidCache.TryGet(clusterIdentity, out var existing))
            {
                context.Respond(new PidResult(existing));
                return Task.CompletedTask;
            }

            if (!_inProgress.Contains(clusterIdentity))
            {
                _inProgress.Add(clusterIdentity);
                context.ReenterAfter(GetWithGlobalLock(context.Sender!, clusterIdentity, msg.CancellationToken), task => {
                        try
                        {
                            var response = new PidResult(task.Result);
                            context.Respond(response);
                            RespondToWaitingRequests(context, clusterIdentity, response);
                            
                            return Task.CompletedTask;
                        }
                        finally
                        {
                            _inProgress.Remove(clusterIdentity);
                        }
                    }
                );
            }
            else
            {
                if (_waitingRequests.TryGetValue(clusterIdentity, out var senders))
                {
                    senders.Add(context.Sender);
                }
                else
                {
                    _waitingRequests[clusterIdentity] = new List<PID> {context.Sender};
                }
            }

            return Task.CompletedTask;


        }

        private void RespondToWaitingRequests(IContext context, ClusterIdentity clusterIdentity, PidResult response)
        {
            if (!_waitingRequests.Remove(clusterIdentity, out var senders)) return;

            foreach (var sender in senders)
            {
                context.Send(sender, response);
            }
        }

        private async Task<PID?> GetWithGlobalLock(PID sender, ClusterIdentity clusterIdentity, CancellationToken ct)
        {
            var tries = 0;
            PID? result = null;
            while (result == null && !ct.IsCancellationRequested && !_cluster.System.Shutdown.IsCancellationRequested && ++tries <= MaxSpawnRetries)
            {
                try
                {
                    var activation = await _storage.TryGetExistingActivation(clusterIdentity, ct);

                    //we got an existing activation, use this
                    if (activation != null)
                    {
                        var existingPid = await ValidateAndMapToPid(clusterIdentity, activation);
                        if (existingPid != null) return existingPid;
                    }

                    //are there any members that can spawn this kind?
                    //if not, just bail out
                    var activator = _memberList.GetActivator(clusterIdentity.Kind, sender.Address);
                    if (activator == null || ct.IsCancellationRequested) return null;

                    //try to acquire global lock
                    var spawnLock = await _storage.TryAcquireLock(clusterIdentity, ct);

                    //we didn't get the lock, wait for activation to complete
                    if (spawnLock == null)
                    {
                        result = await WaitForActivation(clusterIdentity, ct);
                    }
                    else
                    {
                        //we have the lock, spawn and return
                        result = await SpawnActivationAsync(activator, spawnLock, ct);
                    }
                }
                catch (Exception e)
                {
                    if (_cluster.System.Shutdown.IsCancellationRequested) return null;

                    if (_shouldThrottle().IsOpen())
                        _logger.LogError(e, "Failed to get PID for {ClusterIdentity}", clusterIdentity);

                    await Task.Delay(tries * 20, ct);
                }
            }
            return result;
        }

        private async Task<PID?> WaitForActivation(ClusterIdentity clusterIdentity, CancellationToken ct)
        {
            var activation = _storage.WaitForActivation(clusterIdentity, ct);

            return await ValidateAndMapToPid(
                clusterIdentity,
                await activation
            );
        }

        private async Task<PID?> SpawnActivationAsync(Member activator, SpawnLock spawnLock, CancellationToken ct)
        {
            //we own the lock
            _logger.LogDebug("Storing placement lookup for {Identity} {Kind}", spawnLock.ClusterIdentity.Identity,
                spawnLock.ClusterIdentity.Kind
            );

            var remotePid = _lookup.RemotePlacementActor(activator.Address);
            var req = new ActivationRequest
            {
                ClusterIdentity = spawnLock.ClusterIdentity,
                RequestId = spawnLock.LockId
            };

            try
            {
                var resp = ct == CancellationToken.None
                    ? await _cluster.System.Root.RequestAsync<ActivationResponse>(remotePid, req,
                        _cluster.Config!.TimeoutTimespan
                    )
                    : await _cluster.System.Root.RequestAsync<ActivationResponse>(remotePid, req, ct);

                if (resp.Pid != null)
                {
                    _cluster.PidCache.TryAdd(spawnLock.ClusterIdentity, resp.Pid!);
                    return resp.Pid;
                }
            }
            //TODO: decide if we throw or return null
            catch (TimeoutException)
            {
                _logger.LogDebug("Remote PID request timeout {@Request}", req);
            }
            catch (Exception e)
            {
                if (!_cluster.System.Shutdown.IsCancellationRequested && _shouldThrottle().IsOpen() && _memberList.ContainsMemberId(activator.Id))
                    _logger.LogError(e, "Error occured requesting remote PID {@Request}", req);
            }

            //Clean up our mess..
            await _storage.RemoveLock(spawnLock, ct);
            return null;
        }

        private async Task<PID?> ValidateAndMapToPid(ClusterIdentity clusterIdentity, StoredActivation? activation)
        {
            if (activation?.Pid == null) return null;

            var memberExists = activation.MemberId == null || _memberList.ContainsMemberId(activation.MemberId);
            if (memberExists) return activation.Pid;

            if (StaleMembers.TryAdd(activation.MemberId!))
            {
                _logger.LogWarning(
                    "Found placement lookup for {ClusterIdentity}, but Member {MemberId} is not part of cluster, dropping stale entries",
                    clusterIdentity, activation.MemberId
                );
            }

            //let all requests try to remove, but only log on the first occurrence
            await _storage.RemoveMember(activation.MemberId!, CancellationToken.None);
            return null;
        }
    }
}