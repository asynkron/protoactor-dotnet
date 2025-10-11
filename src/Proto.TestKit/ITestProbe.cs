// -----------------------------------------------------------------------
// <copyright file="ITestProbe.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Proto;
using Proto.Mailbox;

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
    ///     The PID of this probe.
    /// </summary>
    PID Self { get; }

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
    ///     asynchronously gets the next system message of type <typeparamref name="T"/>
    /// </summary>
    /// <typeparam name="T">The system message type</typeparam>
    /// <param name="timeAllowed"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<T> GetNextSystemMessageAsync<T>(TimeSpan? timeAllowed = null,
        CancellationToken cancellationToken = default) where T : SystemMessage;

    /// <summary>
    ///     asynchronously gets the next user message of type <typeparamref name="T"/>
    /// </summary>
    /// <typeparam name="T">The user message type</typeparam>
    /// <param name="timeAllowed"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<T> GetNextUserMessageAsync<T>(TimeSpan? timeAllowed = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     asynchronously gets the next system message of type <typeparamref name="T"/> satisfying a predicate
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="when"></param>
    /// <param name="timeAllowed"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<T> GetNextSystemMessageAsync<T>(Func<T, bool> when, TimeSpan? timeAllowed = null,
        CancellationToken cancellationToken = default) where T : SystemMessage;

    /// <summary>
    ///     asynchronously gets the next user message of type <typeparamref name="T"/> satisfying a predicate
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="when"></param>
    /// <param name="timeAllowed"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<T> GetNextUserMessageAsync<T>(Func<T, bool> when, TimeSpan? timeAllowed = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     asynchronously expects the next system message of type <typeparamref name="T"/>
    /// </summary>
    /// <typeparam name="T">The system message type</typeparam>
    /// <param name="timeAllowed"></param>
    /// <param name="cancellationToken"></param>
    Task ExpectNextSystemMessageAsync<T>(TimeSpan? timeAllowed = null,
        CancellationToken cancellationToken = default) where T : SystemMessage;

    /// <summary>
    ///     asynchronously expects the next user message of type <typeparamref name="T"/>
    /// </summary>
    /// <typeparam name="T">The user message type</typeparam>
    /// <param name="timeAllowed"></param>
    /// <param name="cancellationToken"></param>
    Task ExpectNextUserMessageAsync<T>(TimeSpan? timeAllowed = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     asynchronously expects the next system message of type <typeparamref name="T"/> satisfying a predicate
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="when"></param>
    /// <param name="timeAllowed"></param>
    /// <param name="cancellationToken"></param>
    Task ExpectNextSystemMessageAsync<T>(Func<T, bool> when, TimeSpan? timeAllowed = null,
        CancellationToken cancellationToken = default) where T : SystemMessage;

    /// <summary>
    ///     asynchronously expects the next user message of type <typeparamref name="T"/> satisfying a predicate
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="when"></param>
    /// <param name="timeAllowed"></param>
    /// <param name="cancellationToken"></param>
    Task ExpectNextUserMessageAsync<T>(Func<T, bool> when, TimeSpan? timeAllowed = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     ensures that the probe mailbox is empty
    /// </summary>
    /// <param name="timeAllowed"></param>
    /// <param name="cancellationToken"></param>
    Task ExpectEmptyMailboxAsync(TimeSpan? timeAllowed = null,
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
    ///     starts watching the target actor
    /// </summary>
    /// <param name="pid"></param>
    void Watch(PID pid);

    /// <summary>
    ///     stops watching the target actor
    /// </summary>
    /// <param name="pid"></param>
    void Unwatch(PID pid);

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