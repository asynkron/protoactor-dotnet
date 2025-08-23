// -----------------------------------------------------------------------
// <copyright file="IGossipTransport.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Proto;

namespace Proto.Cluster.Gossip;

/// <summary>
/// Abstraction over sending gossip requests.
/// </summary>
public interface IGossipTransport
{
    /// <summary>
    /// Sends a <see cref="GossipRequest"/> and invokes the callback once a response is available.
    /// </summary>
    /// <param name="context">Actor context used for the request.</param>
    /// <param name="pid">Target gossip actor PID.</param>
    /// <param name="request">The request to send.</param>
    /// <param name="onResponse">Continuation executed when the request completes.</param>
    /// <param name="cancellationToken">Cancellation token controlling the request timeout.</param>
    void Request(
        IContext context,
        PID pid,
        GossipRequest request,
        Func<Task<GossipResponse>, Task> onResponse,
        CancellationToken cancellationToken);
}

/// <summary>
/// Default transport that forwards requests using <see cref="IContext.RequestReenter{T}"/>.
/// </summary>
public sealed class GossipTransport : IGossipTransport
{
    public void Request(
        IContext context,
        PID pid,
        GossipRequest request,
        Func<Task<GossipResponse>, Task> onResponse,
        CancellationToken cancellationToken) =>
        context.RequestReenter(pid, request, onResponse, cancellationToken);
}
