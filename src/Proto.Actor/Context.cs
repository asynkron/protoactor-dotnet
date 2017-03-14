// -----------------------------------------------------------------------
//  <copyright file="Context.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Proto
{
    public interface IContext
    {
        /// <summary>
        ///     Gets the PID for the parent of the current actor.
        /// </summary>
        PID Parent { get; }

        /// <summary>
        ///     Gets the PID for the current actor.
        /// </summary>
        PID Self { get; }

        /// <summary>
        ///     The current message to be processed.
        /// </summary>
        object Message { get; }

        /// <summary>
        ///     Gets the PID of the actor that sent the currently processed message.
        /// </summary>
        PID Sender { get; }

        /// <summary>
        ///     Gets the actor associated with this context.
        /// </summary>
        IActor Actor { get; }

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
        ///     Spawns a new child actor based on props and named with a unique ID.
        /// </summary>
        /// <param name="props">The Props used to spawn the actor</param>
        /// <returns>The PID of the child actor</returns>
        PID Spawn(Props props);

        /// <summary>
        ///     Spawns a new child actor based on props and named using a prefix followed by a unique ID.
        /// </summary>
        /// <param name="props">The Props used to spawn the actor</param>
        /// <param name="prefix">The prefix for the actor name</param>
        /// <returns>The PID of the child actor</returns>
        PID SpawnPrefix(Props props, string prefix);

        /// <summary>
        ///     Spawns a new child actor based on props and named using the specified name.
        /// </summary>
        /// <param name="props">The Props used to spawn the actor</param>
        /// <param name="name">The actor name</param>
        /// <returns>The PID of the child actor</returns>
        PID SpawnNamed(Props props, string name);

        /// <summary>
        ///     Replaces the current behavior stack with the new behavior.
        /// </summary>
        void SetBehavior(Receive behavior);

        /// <summary>
        ///     Pushes the behavior onto the current behavior stack and sets the current Receive handler to the new behavior.
        /// </summary>
        void PushBehavior(Receive behavior);

        /// <summary>
        ///     Reverts to the previous Receive handler.
        /// </summary>
        void PopBehavior();

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
        ///     INotInfluenceReceiveTimeout. Setting a duration of less than 1ms will disable the timer.
        /// </summary>
        /// <param name="duration">The receive timeout duration</param>
        void SetReceiveTimeout(TimeSpan duration);

        Task ReceiveAsync(object message);
        void Tell(PID target, object message);
        void Request(PID target, object message);
        Task<T> RequestAsync<T>(PID target, object message, TimeSpan timeout);
        Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken);
        Task<T> RequestAsync<T>(PID target, object message);
    }
}