// -----------------------------------------------------------------------
// <copyright file="IContext.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Proto;

/// <summary>
///     All contextual information available for a given actor
/// </summary>
public interface IContext : ISenderContext, IReceiverContext, ISpawnerContext, IStopperContext
{
    /// <summary>
    ///     Gets the CancellationToken. Pass this token in long running tasks to stop them when the actor is about to stop
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    ///     Gets the receive timeout. It will be TimeSpan.Zero if receive timeout was not set
    /// </summary>
    TimeSpan ReceiveTimeout { get; }

    /// <summary>
    ///     Gets the PIDs of the actor's children.
    /// </summary>
    IReadOnlyCollection<PID> Children { get; }

    /// <summary>
    ///     Sends a response to the current Sender. If the Sender is null, this call has no effect apart from warning log
    ///     entry.
    /// </summary>
    /// <param name="message">The message to send</param>
    void Respond(object message);

    /// <summary>
    ///     Sends a response to the current Sender, including message header. If the Sender is null, this call has no effect
    ///     apart from warning log entry.
    /// </summary>
    /// <param name="message">The message to send</param>
    /// <param name="header"></param>
    void Respond(object message, MessageHeader header) => Respond(new MessageEnvelope(message, null, header));

    /// <summary>
    ///     Registers the actor as a watcher for the specified PID. When the PID terminates the watcher is notified with
    ///     <see cref="Terminated" /> message.
    /// </summary>
    /// <param name="pid">The PID to watch</param>
    void Watch(PID pid);

    /// <summary>
    ///     Unregisters the actor as a watcher for the specified PID.
    /// </summary>
    /// <param name="pid">The PID to unwatch</param>
    void Unwatch(PID pid);

    /// <summary>
    ///     Sets the receive timeout. If no message is received for the given duration, a <see cref="Proto.ReceiveTimeout" />
    ///     message will be sent
    ///     to the actor. If a message is received within the given duration, the timer is reset, unless the message implements
    ///     <see cref="INotInfluenceReceiveTimeout" />
    /// </summary>
    /// <param name="duration">The receive timeout duration</param>
    void SetReceiveTimeout(TimeSpan duration);

    /// <summary>
    ///     Cancels the receive timeout.
    /// </summary>
    void CancelReceiveTimeout();

    /// <summary>
    ///     Forwards the current message in the context to another actor.
    /// </summary>
    /// <param name="target">Actor to forward to</param>
    void Forward(PID target);

    /// <summary>
    ///     Awaits the given target task and once completed, the given action is then completed within the actors concurrency
    ///     constraint.
    ///     The concept is called Reentrancy, where an actor can continue to process messages while also awaiting that some
    ///     asynchronous operation completes.
    /// </summary>
    /// <param name="target">the Task to await</param>
    /// <param name="action">The continuation to call once the task is completed. The awaited task is passed in as a parameter.</param>
    /// <typeparam name="T">The generic type of the task</typeparam>
    void ReenterAfter<T>(Task<T> target, Func<Task<T>, Task> action);

    /// <summary>
    ///     Awaits the given target task and once completed, the given action is then completed within the actors concurrency
    ///     constraint.
    ///     The concept is called Reentrancy, where an actor can continue to process messages while also awaiting that some
    ///     asynchronous operation completes.
    /// </summary>
    /// <param name="target">The Task to await</param>
    /// <param name="action">The continuation to call once the task is completed</param>
    void ReenterAfter(Task target, Action action);

    /// <summary>
    ///     Awaits the given target task and once completed, the given action is then completed within the actors concurrency
    ///     constraint.
    ///     The concept is called Reentrancy, where an actor can continue to process messages while also awaiting that some
    ///     asynchronous operation completes.
    /// </summary>
    /// <param name="target">The Task to await</param>
    /// <param name="action">The continuation to call once the task is completed. The awaited task is passed in as a parameter.</param>
    void ReenterAfter(Task target, Action<Task> action);

    /// <summary>
    ///     Awaits the given target task and once completed, the given action is then completed within the actors concurrency
    ///     constraint.
    ///     The concept is called Reentrancy, where an actor can continue to process messages while also awaiting that some
    ///     asynchronous operation completes.
    /// </summary>
    /// <param name="target">The Task to await</param>
    /// <param name="action">The continuation to call once the task is completed. The awaited task is passed in as a parameter.</param>
    void ReenterAfter<T>(Task<T> target, Action<Task<T>> action);

    /// <summary>
    ///     Awaits the given target task and once completed, the given action is then completed within the actors concurrency
    ///     constraint.
    ///     The concept is called Reentrancy, where an actor can continue to process messages while also awaiting that some
    ///     asynchronous operation completes.
    /// </summary>
    /// <param name="target">The Task to await</param>
    /// <param name="action">The continuation to call once the task is completed. The awaited task is passed in as a parameter.</param>
    void ReenterAfter(Task target, Func<Task, Task> action);

    /// <summary>
    ///     Captures the current MessageOrEnvelope for the ActorContext. Use this to stash messages for later processing. Use
    ///     <see cref="Apply" />
    ///     to process stored messages.
    /// </summary>
    /// <returns>The Captured Context</returns>
    CapturedContext Capture();

    /// <summary>
    ///     Apply a captured context
    ///     This overwrites the context current state with the state from the captured context
    /// </summary>
    /// <param name="capturedContext">The context to apply</param>
    void Apply(CapturedContext capturedContext);

    /// <summary>
    ///     Calls the callback when specified cancellation token gets cancelled. The callback runs within actor's concurrency
    ///     constrins.
    ///     If CancellationToken is non-cancellable, this is a noop.
    /// </summary>
    /// <param name="cancellationToken">The CancellationToken to continue after</param>
    /// <param name="onCancelled">The callback</param>
    void ReenterAfterCancellation(CancellationToken cancellationToken, Action onCancelled);
}