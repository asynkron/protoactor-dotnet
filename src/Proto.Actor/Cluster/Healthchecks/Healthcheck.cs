// -----------------------------------------------------------------------
// <copyright file = "Healthcheck.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Proto.Cluster;

/// <summary>
///     Reports unhealthy status if the <see cref="Cluster" /> is shutting down or completed the shutdown.
///     Reports degraded status if the <see cref="Cluster" /> has not joined the cluster yet.
/// </summary>
[PublicAPI]
public class ClusterHealthCheck : IHealthCheck
{
    private readonly Cluster _cluster;

    public ClusterHealthCheck(Cluster cluster)
    {
        _cluster = cluster;
        _cluster.System.Diagnostics.RegisterEvent("Cluster", "HealthChecks enabled");
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = new())
    {
        if (_cluster.System.Shutdown.IsCancellationRequested)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("ActorSystem has been stopped"));
        }

        if (_cluster.ShutdownCompleted.IsCompleted)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Member has stopped"));
        }

        if (_cluster.MemberList?.Stopping == true)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Member is stopping"));
        }

        return Task.FromResult(_cluster.JoinedCluster.IsCompletedSuccessfully
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Degraded("Member has not joined cluster yet"));
    }
}