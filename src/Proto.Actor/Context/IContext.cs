// -----------------------------------------------------------------------
// <copyright file="IContext.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Proto
{
    public interface IContext : ISenderContext, IReceiverContext, ISpawnerContext, IStopperContext
    {
        /// <summary>
        ///     Gets the CancellationToken. Pass this token in long running tasks to stop them when the actor is about to stop
        /// </summary>
        CancellationToken CancellationToken { get; }

        /// <summary>
        ///     Gets the receive timeout.
        /// </summary>
        TimeSpan ReceiveTimeout { get; }

        /// <summary>
        ///     Gets the PIDs of the actor's children.
        /// </summary>
        IReadOnlyCollection<PID> Children { get; }

        /// <summary>
        ///     Sends a response to the current Sender. If the Sender is null, the actor will panic.
        /// </summary>
        /// <param name="message">The message to send</param>
        void Respond(object message);

        /// <summary>
        ///     Stashes the current message on a stack for re-processing when the actor restarts.
        /// </summary>
        void Stash();

        /// <summary>
        ///     Registers the actor as a watcher for the specified PID.
        /// </summary>
        /// <param name="pid">The PID to watch</param>
        void Watch(PID pid);

        /// <summary>
        ///     Unregisters the actor as a watcher for the specified PID.
        /// </summary>
        /// <param name="pid">The PID to unwatch</param>
        void Unwatch(PID pid);

        /// <summary>
        ///     Sets the receive timeout. If no message is received for the given duration, a ReceiveTimeout message will be sent
        ///     to the actor. If a message is received within the given duration, the timer is reset, unless the message implements
        ///     INotInfluenceReceiveTimeout.
        /// </summary>
        /// <param name="duration">The receive timeout duration</param>
        void SetReceiveTimeout(TimeSpan duration);

        void CancelReceiveTimeout();

        void Forward(PID target);

        /// <summary>
        ///     Awaits the given target task and once completed, the given action is then completed within the actors concurrency
        ///     constraint.
        ///     The concept is called Reentrancy, where an actor can continue to process messages while also awaiting that some
        ///     asynchronous operation completes.
        /// </summary>
        /// <param name="target">the Task to await</param>
        /// <param name="action">the continuation to call once the task is completed</param>
        /// <typeparam name="T">The generic type of the task</typeparam>
        void ReenterAfter<T>(Task<T> target, Func<Task<T>, Task> action);

        /// <summary>
        ///     Awaits the given target task and once completed, the given action is then completed within the actors concurrency
        ///     constraint.
        ///     The concept is called Reentrancy, where an actor can continue to process messages while also awaiting that some
        ///     asynchronous operation completes.
        /// </summary>
        /// <param name="target">the Task to await</param>
        /// <param name="action">the continuation to call once the task is completed</param>
        void ReenterAfter(Task target, Action action);
    }
}