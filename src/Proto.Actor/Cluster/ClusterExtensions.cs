// -----------------------------------------------------------------------
// <copyright file="ClusterExtensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;

namespace Proto.Cluster;

public static class ClusterExtensions
{
    /// <summary>
    ///     Resolves cluster identity to a <see cref="PID" />. The cluster identity will be activated if it is not already.
    /// </summary>
    /// <param name="cluster"></param>
    /// <param name="identity">Identity</param>
    /// <param name="kind">Cluster kind</param>
    /// <param name="ct">Token to cancel the operation</param>
    /// <returns></returns>
    public static Task<PID?> GetAsync(this Cluster cluster, string identity, string kind, CancellationToken ct) =>
        cluster.GetAsync(new ClusterIdentity { Identity = identity, Kind = kind }, ct);

    /// <summary>
    ///     Sends a request to a virtual actor.
    /// </summary>
    /// <param name="cluster"></param>
    /// <param name="identity">Identity of the actor</param>
    /// <param name="kind">Cluster kind of the actor</param>
    /// <param name="message">Message to send</param>
    /// <param name="ct">Token to cancel the operation</param>
    /// <typeparam name="T">Expected response type</typeparam>
    /// <returns>Response of null if timed out</returns>
    public static Task<T> RequestAsync<T>(this Cluster cluster, string identity, string kind, object message,
        CancellationToken ct) =>
        cluster.RequestAsync<T>(new ClusterIdentity { Identity = identity, Kind = kind }, message,
            cluster.System.Root, ct);

    /// <summary>
    ///     Sends a request to a virtual actor.
    /// </summary>
    /// <param name="cluster"></param>
    /// <param name="identity">Identity of the actor</param>
    /// <param name="kind">Cluster kind of the actor</param>
    /// <param name="message">Message to send</param>
    /// <param name="context">Sender context to send the message through</param>
    /// <param name="ct">Token to cancel the operation</param>
    /// <typeparam name="T">Expected response type</typeparam>
    /// <returns>Response of null if timed out</returns>
    public static Task<T> RequestAsync<T>(this Cluster cluster, string identity, string kind, object message,
        ISenderContext context, CancellationToken ct) =>
        cluster.RequestAsync<T>(new ClusterIdentity { Identity = identity, Kind = kind }, message, context, ct);

    /// <summary>
    ///     Sends a request to a virtual actor.
    /// </summary>
    /// <param name="cluster"></param>
    /// <param name="clusterIdentity">Cluster identity of the actor</param>
    /// <param name="message">Message to send</param>
    /// <param name="ct">Token to cancel the operation</param>
    /// <typeparam name="T">Expected response type</typeparam>
    /// <returns>Response of null if timed out</returns>
    public static Task<T> RequestAsync<T>(this Cluster cluster, ClusterIdentity clusterIdentity, object message,
        CancellationToken ct) =>
        cluster.RequestAsync<T>(clusterIdentity, message, cluster.System.Root, ct);
}