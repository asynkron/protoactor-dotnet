// -----------------------------------------------------------------------
// <copyright file="ISpawnerContext.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

// ReSharper disable once CheckNamespace
namespace Proto
{
    public interface ISpawnerContext
    {
        /// <summary>
        ///     Spawns a new child actor based on props and named with a unique ID.
        /// </summary>
        /// <param name="props">The Props used to spawn the actor</param>
        /// <returns>The PID of the child actor</returns>
        PID Spawn(Props props);

        /// <summary>
        ///     Spawns a new child actor based on props and named using the specified name.
        /// </summary>
        /// <param name="props">The Props used to spawn the actor</param>
        /// <param name="name">The actor name</param>
        /// <returns>The PID of the child actor</returns>
        PID SpawnNamed(Props props, string name);

        /// <summary>
        ///     Spawns a new child actor based on props and named using a prefix followed by a unique ID.
        /// </summary>
        /// <param name="props">The Props used to spawn the actor</param>
        /// <param name="prefix">The prefix for the actor name</param>
        PID SpawnPrefix(Props props, string prefix);
    }
}