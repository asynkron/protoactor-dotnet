// -----------------------------------------------------------------------
// <copyright file="IdentityStorageWorker.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Utils;

namespace Proto.Cluster.Identity;

internal class IdentityStorageWorker : IActor
{
    private const int MaxSpawnRetries = 3;

    private static readonly ConcurrentSet<string> StaleMembers = new();

    private readonly Cluster _cluster;

    private readonly HashSet<ClusterIdentity> _inProgress = new();
    private readonly ILogger _logger = Log.CreateLogger<IdentityStorageWorker>();
    private readonly IdentityStorageLookup _lookup;
    private readonly MemberList _memberList;

    private readonly ShouldThrottle _shouldThrottle;
    private readonly IIdentityStorage _storage;
    private readonly Dictionary<ClusterIdentity, List<PID>> _waitingRequests = new();

    public IdentityStorageWorker(IdentityStorageLookup storageLookup)
    {
        _shouldThrottle = Throttle.Create(
            10,
            TimeSpan.FromSeconds(5),
            i => _logger.LogInformation("Throttled {LogCount} IdentityStorageWorker logs", i)
        );

        _cluster = storageLookup.Cluster;
        _memberList = storageLookup.MemberList;
        _lookup = storageLookup;
        _storage = storageLookup.Storage;
    }

    public Task ReceiveAsync(IContext context)
    {
        if (context.Message is not GetPid msg)
        {
            return Task.CompletedTask;
        }

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

            context.ReenterAfter(GetWithGlobalLock(context.Sender!, clusterIdentity), task =>
                {
                    try
                    {
                        var response = task.Result;
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
                _waitingRequests[clusterIdentity] = new List<PID> { context.Sender };
            }
        }

        return Task.CompletedTask;
    }

    private void RespondToWaitingRequests(IContext context, ClusterIdentity clusterIdentity, PidResult response)
    {
        if (!_waitingRequests.Remove(clusterIdentity, out var senders))
        {
            return;
        }

        foreach (var sender in senders)
        {
            context.Send(sender, response);
        }
    }

    private Task<PidResult> GetWithGlobalLock(PID sender, ClusterIdentity clusterIdentity)
    {
        async Task<PidResult> Inner()
        {
            var tries = 0;
            PID? pid = null;
            SpawnLock? spawnLock = null;

            while (pid == null && !_cluster.System.Shutdown.IsCancellationRequested && ++tries <= MaxSpawnRetries)
            {
                try
                {
                    using var tryGetCts = new CancellationTokenSource(_cluster.Config.ActorActivationTimeout);
                    var activation = await _storage.TryGetExistingActivation(clusterIdentity, tryGetCts.Token).ConfigureAwait(false);

                    //we got an existing activation, use this
                    if (activation != null)
                    {
                        var existingPid = await ValidateAndMapToPid(clusterIdentity, activation).ConfigureAwait(false);

                        if (existingPid != null)
                        {
                            return new PidResult(existingPid);
                        }
                    }

                    //are there any members that can spawn this kind?
                    //if not, just bail out

                    var activator = _memberList.GetActivator(clusterIdentity.Kind, sender.Address);

                    if (activator == null)
                    {
                        return null!;
                    }

                    //try to acquire global lock
                    spawnLock ??= await TryAcquireLock(clusterIdentity).ConfigureAwait(false);

                    //we didn't get the lock, wait for activation to complete

                    if (spawnLock == null)
                    {
                        using var cts = new CancellationTokenSource(_cluster.Config.ActorActivationTimeout);
                        pid = await WaitForActivation(clusterIdentity, cts.Token).ConfigureAwait(false);
                    }
                    else
                    {
                        using var cts = new CancellationTokenSource(_cluster.Config.ActorActivationTimeout);
                        //we have the lock, spawn and return
                        (var spawnResult, spawnLock) = await SpawnActivationAsync(activator, spawnLock, cts.Token).ConfigureAwait(false);

                        if (spawnResult is not null)
                        {
                            return spawnResult;
                        }
                    }
                }
                catch (OperationCanceledException e)
                {
                    if (_cluster.System.Shutdown.IsCancellationRequested)
                    {
                        return null!;
                    }

                    if (_shouldThrottle().IsOpen())
                    {
                        _logger.LogWarning(e, "Failed to get PID for {ClusterIdentity}", clusterIdentity);
                    }

                    await Task.Delay(tries * 20).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    if (_cluster.System.Shutdown.IsCancellationRequested)
                    {
                        return null!;
                    }

                    if (_shouldThrottle().IsOpen())
                    {
                        _logger.LogError(e, "Failed to get PID for {ClusterIdentity}", clusterIdentity);
                    }

                    await Task.Delay(tries * 20).ConfigureAwait(false);
                }
            }

            return new PidResult(pid);
        }

        if (!_cluster.System.Metrics.Enabled)
        {
            return Inner();
        }

        return IdentityMetrics.GetWithGlobalLockDuration.Observe(
            Inner,
            new KeyValuePair<string, object?>("id", _cluster.System.Id),
            new KeyValuePair<string, object?>("address", _cluster.System.Address),
            new KeyValuePair<string, object?>("clusterkind", clusterIdentity.Kind)
        );
    }

    private Task<SpawnLock?> TryAcquireLock(ClusterIdentity clusterIdentity)
    {
        Task<SpawnLock?> Inner() => _storage.TryAcquireLock(clusterIdentity, CancellationTokens.FromSeconds(5));

        if (!_cluster.System.Metrics.Enabled)
        {
            return Inner();
        }

        return IdentityMetrics.TryAcquireLockDuration.Observe(
            Inner,
            new KeyValuePair<string, object?>("id", _cluster.System.Id),
            new KeyValuePair<string, object?>("address", _cluster.System.Address),
            new KeyValuePair<string, object?>("clusterkind", clusterIdentity.Kind)
        );
    }

    private Task<PID?> WaitForActivation(ClusterIdentity clusterIdentity, CancellationToken ct)
    {
        async Task<PID?> Inner()
        {
            var activation = await _storage.WaitForActivation(clusterIdentity, ct).ConfigureAwait(false);

            var res = await ValidateAndMapToPid(
                clusterIdentity,
                activation
            ).ConfigureAwait(false);

            return res;
        }

        if (!_cluster.System.Metrics.Enabled)
        {
            return Inner();
        }

        return IdentityMetrics.WaitForActivationDuration
            .Observe(
                Inner,
                new KeyValuePair<string, object?>("id", _cluster.System.Id),
                new KeyValuePair<string, object?>("address", _cluster.System.Address),
                new KeyValuePair<string, object?>("clusterkind", clusterIdentity.Kind)
            );
    }

    private async Task<(PidResult?, SpawnLock?)> SpawnActivationAsync(Member activator, SpawnLock spawnLock,
        CancellationToken ct)
    {
        //we own the lock
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Storing placement lookup for {Identity} {Kind}", spawnLock.ClusterIdentity.Identity,
                spawnLock.ClusterIdentity.Kind
            );
        }

        var remotePid = _lookup.RemotePlacementActor(activator.Address);

        var req = new ActivationRequest
        {
            ClusterIdentity = spawnLock.ClusterIdentity,
            RequestId = spawnLock.LockId
        };

        try
        {
            var resp = await _cluster.System.Root.RequestAsync<ActivationResponse>(remotePid, req, ct).ConfigureAwait(false);

            if (resp.Pid != null)
            {
                _cluster.PidCache.TryAdd(spawnLock.ClusterIdentity, resp.Pid!);

                return (new PidResult(resp.Pid), null);
            }

            if (resp.InvalidIdentity)
            {
                return (PidResult.Blocked, null);
            }
        }
        //TODO: decide if we throw or return null
        catch (DeadLetterException)
        {
            if (!_cluster.System.Shutdown.IsCancellationRequested && _shouldThrottle().IsOpen())
            {
                _logger.LogWarning("[SpawnActivationAsync] Member {Activator} unavailable", activator);
            }

            return (null, spawnLock);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("[SpawnActivationAsync] Remote PID request timeout {@Request}", req);
        }
        catch (Exception e)
        {
            if (!_cluster.System.Shutdown.IsCancellationRequested && _shouldThrottle().IsOpen() &&
                _memberList.ContainsMemberId(activator.Id))
            {
                _logger.LogError(e, "[SpawnActivationAsync] Error occured requesting remote PID {@Request}", req);
            }
        }

        //Clean up our mess..
        await _storage.RemoveLock(spawnLock, ct).ConfigureAwait(false);

        return (null, null);
    }

    private async Task<PID?> ValidateAndMapToPid(ClusterIdentity clusterIdentity, StoredActivation? activation)
    {
        if (activation?.Pid == null)
        {
            return null;
        }

        var memberExists = _memberList.ContainsMemberId(activation.MemberId);

        if (memberExists)
        {
            return activation.Pid;
        }

        if (StaleMembers.TryAdd(activation.MemberId))
        {
            _logger.LogWarning(
                "Found placement lookup for {ClusterIdentity}, but Member {MemberId} is not part of cluster, dropping stale entries",
                clusterIdentity, activation.MemberId
            );
        }

        //let all requests try to remove, but only log on the first occurrence
        await _storage.RemoveMember(activation.MemberId, CancellationToken.None).ConfigureAwait(false);

        return null;
    }
}