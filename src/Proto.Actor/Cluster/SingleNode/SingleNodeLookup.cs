// -----------------------------------------------------------------------
// <copyright file="SingleNodeLookup.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Cluster.Identity;

namespace Proto.Cluster.SingleNode;

/// <summary>
///     Provides a lookup optimized for single node 'clusters'
///     Only usable with SingleNodeProvider
/// </summary>
public class SingleNodeLookup : IIdentityLookup
{
    private const string ActivatorActorName = "$sn-activator";

    private static readonly ILogger Logger = Log.CreateLogger<SingleNodeLookup>();
    private readonly TimeSpan _getPidTimeout;
    private PID _activatorActor = null!;
    private Cluster _cluster = null!;

    public SingleNodeLookup() : this(TimeSpan.FromSeconds(1))
    {
    }

    public SingleNodeLookup(TimeSpan getPidTimeout)
    {
        _getPidTimeout = getPidTimeout;
    }

    public async Task<PID?> GetAsync(ClusterIdentity clusterIdentity, CancellationToken notUsed)
    {
        using var cts = new CancellationTokenSource(_getPidTimeout);

        var req = new ActivationRequest
        {
            ClusterIdentity = clusterIdentity
        };

        try
        {
            var resp = await _cluster.System.Root.RequestAsync<ActivationResponse>(_activatorActor, req, cts.Token).ConfigureAwait(false);

            if (resp.InvalidIdentity)
            {
                throw new IdentityIsBlockedException(clusterIdentity);
            }

            return resp?.Pid;
        }
        catch (DeadLetterException)
        {
            Logger.LogInformation("[SingleNode] Remote PID request deadletter {@Request}", req);

            return null;
        }
        catch (TimeoutException)
        {
            Logger.LogInformation("[SingleNode] Remote PID request timeout {@Request}", req);

            return null;
        }
        catch (Exception e) when (e is not IdentityIsBlockedException)
        {
            e.CheckFailFast();
            Logger.LogError(e, "[SingleNode] Error occured requesting remote PID {@Request}", req);

            return null;
        }
    }

    public Task RemovePidAsync(ClusterIdentity clusterIdentity, PID pid, CancellationToken ct)
    {
        var activationTerminated = new ActivationTerminated
        {
            Pid = pid,
            ClusterIdentity = clusterIdentity
        };

        _cluster.MemberList.BroadcastEvent(activationTerminated);

        return Task.CompletedTask;
    }

    public Task SetupAsync(Cluster cluster, string[] kinds, bool isClient)
    {
        if (cluster.Provider is not SingleNodeProvider || isClient)
        {
            throw new ArgumentException("SingleNodeLookup can only be used with SingleNodeProvider in server mode");
        }

        _cluster = cluster;
        var props = Props.FromProducer(() => new SingleNodeActivatorActor(_cluster));
        _activatorActor = cluster.System.Root.SpawnNamedSystem(props, ActivatorActorName);

        return Task.CompletedTask;
    }

    public Task ShutdownAsync() => _cluster.System.Root.StopAsync(_activatorActor);
}