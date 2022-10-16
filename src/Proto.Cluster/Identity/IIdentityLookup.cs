// -----------------------------------------------------------------------
// <copyright file="IIdentityLookup.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using Proto.Diagnostics;

namespace Proto.Cluster.Identity;

/// <summary>
///     Identity lookup is used to activate and locate virtual actor activations in the cluster.
///     See <a href="https://proto.actor/docs/cluster/identity-lookup-net/">Identity Lookup docs</a> for more details.
/// </summary>
public interface IIdentityLookup : IDiagnosticsProvider
{
    /// <summary>
    ///     Activates or locates a virtual actor in the cluster.
    /// </summary>
    /// <param name="clusterIdentity">Actor's cluster identity</param>
    /// <param name="ct">Token to cancel the operation</param>
    /// <returns></returns>
    Task<PID?> GetAsync(ClusterIdentity clusterIdentity, CancellationToken ct);

    /// <summary>
    ///     Removes the virtual actor <see cref="PID" /> from the lookup. Called after the actor is terminated.
    /// </summary>
    /// <param name="clusterIdentity">Actor's cluster identity</param>
    /// <param name="pid">PID to remove</param>
    /// <param name="ct">Token to cancel the operation</param>
    /// <returns></returns>
    Task RemovePidAsync(ClusterIdentity clusterIdentity, PID pid, CancellationToken ct);

    /// <summary>
    ///     Starts the lookup.
    /// </summary>
    /// <param name="cluster"></param>
    /// <param name="kinds">Cluster kinds supported by this member</param>
    /// <param name="isClient">In client mode, no actors are activated on this member</param>
    /// <returns></returns>
    Task SetupAsync(Cluster cluster, string[] kinds, bool isClient);

    /// <summary>
    ///     Shutdowns the lookup.
    /// </summary>
    /// <returns></returns>
    Task ShutdownAsync();
}