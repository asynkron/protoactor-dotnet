// -----------------------------------------------------------------------
// <copyright file="ITestProbe.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.TestKit;

/// <summary>
///     a test probe for intercepting messages
/// </summary>
public interface ITestProbe
{
    /// <summary>
    ///     the sender of the last message retrieved from GetNextMessageAsync or FishForMessageAsync
    /// </summary>
    PID? Sender { get; }

    /// <summary>
    ///     the context of the test probe
    /// </summary>
    IContext? Context { get; }

    /// <summary>
    ///     asynchronously checks that no message arrives within the time allowed
    /// </summary>
    /// <param name="timeAllowed"></param>
    /// <param name="cancellationToken"></param>
    Task ExpectNoMessageAsync(TimeSpan? timeAllowed = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     asynchronously gets the next message from the test probe
    /// </summary>
    /// <param name="timeAllowed"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<object?> GetNextMessageAsync(TimeSpan? timeAllowed = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     asynchronously gets the next message from the test probe
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="timeAllowed"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<T> GetNextMessageAsync<T>(TimeSpan? timeAllowed = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     asynchronously gets the next message from the test probe
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="when"></param>
    /// <param name="timeAllowed"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<T> GetNextMessageAsync<T>(Func<T, bool> when, TimeSpan? timeAllowed = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     asynchronously fishes for the next message of a given type from the test probe
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="timeAllowed"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<T> FishForMessageAsync<T>(TimeSpan? timeAllowed = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     asynchronously fishes for the next message of a given type from the test probe
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="when"></param>
    /// <param name="timeAllowed"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<T> FishForMessageAsync<T>(Func<T, bool> when, TimeSpan? timeAllowed = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     sends a message from the test probe to the target
    /// </summary>
    /// <param name="target"></param>
    /// <param name="message"></param>
    void Send(PID target, object message);

    /// <summary>
    ///     responds to the current sender
    /// </summary>
    /// <param name="message"></param>
    void Respond(object message);

    /// <summary>
    ///     sends a request message from the test probe to the target
    /// </summary>
    /// <param name="target"></param>
    /// <param name="message"></param>
    void Request(PID target, object message);

    /// <summary>
    ///     requests a message from the target
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="target"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    Task<T> RequestAsync<T>(PID target, object message);

    /// <summary>
    ///     requests a message from the target
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="target"></param>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken);

    /// <summary>
    ///     requests a message from the target
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="target"></param>
    /// <param name="message"></param>
    /// <param name="timeAllowed"></param>
    /// <returns></returns>
    Task<T> RequestAsync<T>(PID target, object message, TimeSpan timeAllowed);
}