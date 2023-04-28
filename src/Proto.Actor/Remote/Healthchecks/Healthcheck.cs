// -----------------------------------------------------------------------
// <copyright file = "Healthcheck.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Proto.Remote.HealthChecks;

/// <summary>
///     Reports unhealthy status if the <see cref="ActorSystem" /> is shutting down or completed the shutdown.
/// </summary>
[PublicAPI]
public class ActorSystemHealthCheck : IHealthCheck
{
    private readonly ActorSystem _system;

    public ActorSystemHealthCheck(ActorSystem system)
    {
        _system = system;
        _system.Diagnostics.RegisterEvent("Remote", "HealthChecks enabled");
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = new()) =>
        _system.Shutdown.IsCancellationRequested switch
        {
            true => Task.FromResult(HealthCheckResult.Unhealthy("ActorSystem has been stopped")),
            _    => Task.FromResult(HealthCheckResult.Healthy())
        };
}