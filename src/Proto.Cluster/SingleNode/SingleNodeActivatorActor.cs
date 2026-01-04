// -----------------------------------------------------------------------
// <copyright file="SingleNodeActivatorActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Cluster.Identity;

namespace Proto.Cluster.SingleNode;

internal class SingleNodeActivatorActor : BaseActivatorActor
{
    private static readonly ILogger _logger = Log.CreateLogger<SingleNodeActivatorActor>();

    public SingleNodeActivatorActor(Cluster cluster) : base(cluster)
    {
    }

    protected override ILogger Logger => _logger;
    protected override string LogPrefix => "[SingleNode]";

    public override Task ReceiveAsync(IContext context) =>
        context.Message switch
        {
            Started                   => OnStarted(context),
            Stopping                  => OnStopping(context),
            ActivationRequest msg     => OnActivationRequest(msg, context),
            ActivationTerminated msg  => OnActivationTerminated(msg),
            ActivationTerminating msg => OnActivationTerminating(msg),
            _                         => Task.CompletedTask
        };

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
        Logger.LogInformation("{LogPrefix} - Stopped {ActorCount} actors", LogPrefix, clusterIdentities.Count);
    }

    private async Task OnActivationRequest(ActivationRequest msg, IContext context)
    {
        if (context.System.Metrics.Enabled)
        {
            IdentityMetrics.RecordActivationRequestReceived(context.System, msg.Kind);
        }

        await TrySpawnOrVerifyAsync(msg, context).ConfigureAwait(false);
    }
}
