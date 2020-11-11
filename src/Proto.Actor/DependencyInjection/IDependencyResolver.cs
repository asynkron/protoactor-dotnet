//-----------------------------------------------------------------------
// <copyright file="IDependencyResolver.cs">
//     Copyright (C) 2009-2020 Lightbend Inc.   <http://www.lightbend.com>
//     Copyright (C) 2013-2020 .NET Foundation  <https://github.com/akkadotnet/akka.net>
//     Copyright (C) 2020-2020 Asynkron AB      <https://proto.actor>
// </copyright>
//-----------------------------------------------------------------------

using System;

namespace Proto.DependencyInjection
{
    public interface IDependencyResolver
    {
        // /// <summary>
        // /// Retrieves an actor's type with the specified name
        // /// </summary>
        // /// <param name="actorName">The name of the actor to retrieve</param>
        // /// <returns>The type with the specified actor name</returns>
        // Type GetType(string actorName);
        // /// <summary>
        // /// Creates a delegate factory used to create actors based on their type
        // /// </summary>
        // /// <param name="actorType">The type of actor that the factory builds</param>
        // /// <returns>A delegate factory used to create actors</returns>
        // Func<IActor> CreateActorFactory(Type actorType);
        /// <summary>
        /// Used to register the configuration for an actor of the specified type <typeparamref name="TActor"/>
        /// </summary>
        /// <typeparam name="TActor">The type of actor the configuration is based</typeparam>
        /// <returns>The configuration object for the given actor type</returns>
        Props PropsFor<TActor>() where TActor : IActor;
        /// <summary>
        /// Used to register the configuration for an actor of the specified type <paramref name="actorType"/> 
        /// </summary>
        /// <param name="actorType">The <see cref="Type"/> of actor the configuration is based</param>
        /// <returns>The configuration object for the given actor type</returns>
        Props PropsFor(Type actorType);
        /// <summary>
        /// Signals the container to release it's reference to the actor.
        /// </summary>
        /// <param name="actor">The actor to remove from the container</param>
        void Release(IActor actor);
    }
}