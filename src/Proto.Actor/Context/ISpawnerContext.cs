// -----------------------------------------------------------------------
// <copyright file="ISpawnerContext.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

// ReSharper disable once CheckNamespace

using System;

namespace Proto;

public interface ISpawnerContext : ISystemContext
{
    /// <summary>
    ///     Spawns a new child actor based on props and specified name.
    /// </summary>
    /// <param name="props">The Props used to spawn the actor</param>
    /// <param name="name">The actor name</param>
    /// <param name="callback"></param>
    /// <returns>The PID of the child actor</returns>
    PID SpawnNamed(Props props, string name, Action<IContext>? callback = null);
}

public static class SpawnerContextExtensions
{
    /// <summary>
    ///     Spawns a new child actor based on props and named with a unique ID.
    /// </summary>
    /// <param name="self"></param>
    /// <param name="props">The Props used to spawn the actor</param>
    /// <returns>The PID of the child actor</returns>
    public static PID Spawn(this ISpawnerContext self, Props props) => self.SpawnNamed(props, "");

    /// <summary>
    ///     Spawns a new child actor based on props and named with a unique ID.
    /// </summary>
    /// <param name="self"></param>
    /// <param name="props">The Props used to spawn the actor</param>
    /// <param name="callback"></param>
    /// <returns>The PID of the child actor</returns>
    public static PID Spawn(this ISpawnerContext self, Props props, Action<IContext> callback) =>
        self.SpawnNamed(props, "", callback);

    /// <summary>
    ///     Spawns a new child actor based on props and named using a prefix followed by a unique ID.
    /// </summary>
    /// <param name="self"></param>
    /// <param name="props">The Props used to spawn the actor</param>
    /// <param name="prefix">The prefix for the actor name</param>
    public static PID SpawnPrefix(this ISpawnerContext self, Props props, string prefix)
    {
        var name = prefix + self.System.ProcessRegistry.NextId();

        return self.SpawnNamed(props, name);
    }

    /// <summary>
    ///     Spawns a new child actor based on props and named using a prefix followed by a unique ID.
    /// </summary>
    /// <param name="self"></param>
    /// <param name="props">The Props used to spawn the actor</param>
    /// <param name="prefix">The prefix for the actor name</param>
    /// <param name="callback"></param>
    public static PID SpawnPrefix(this ISpawnerContext self, Props props, string prefix, Action<IContext> callback)
    {
        var name = prefix + self.System.ProcessRegistry.NextId();

        return self.SpawnNamed(props, name, callback);
    }
}