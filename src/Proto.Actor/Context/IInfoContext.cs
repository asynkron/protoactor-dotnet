// -----------------------------------------------------------------------
// <copyright file="IInfoContext.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

// ReSharper disable once CheckNamespace
namespace Proto
{
    public interface IInfoContext
    {
        /// <summary>
        ///     Gets the PID for the parent of the current actor.
        /// </summary>
        PID? Parent { get; }

        /// <summary>
        ///     Gets the PID for the current actor.
        /// </summary>
        PID? Self { get; }

        /// <summary>
        ///     Gets the PID of the actor that sent the currently processed message.
        /// </summary>
        PID? Sender { get; }

        /// <summary>
        ///     Gets the actor associated with this context.
        /// </summary>
        IActor? Actor { get; }

        /// <summary>
        ///     Gets the actor system this actor was spawned in.
        /// </summary>
        ActorSystem System { get; }
    }
}