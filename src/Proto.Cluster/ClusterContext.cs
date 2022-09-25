// -----------------------------------------------------------------------
// <copyright file="RequestAsyncStrategy.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;

namespace Proto.Cluster;

public interface IClusterContext
{
    /// <summary>
    ///     Sends a request to a cluster identity
    /// </summary>
    /// <param name="clusterIdentity">Cluster identity to call</param>
    /// <param name="message">Message to send</param>
    /// <param name="context"><see cref="ISenderContext" /> to send the message through</param>
    /// <param name="ct">Token to cancel the request</param>
    /// <typeparam name="T">Type of the expected response</typeparam>
    /// <returns>Response or null if timed out</returns>
    Task<T?> RequestAsync<T>(ClusterIdentity clusterIdentity, object message, ISenderContext context,
        CancellationToken ct);
}