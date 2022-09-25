// -----------------------------------------------------------------------
// <copyright file="IReceiverContext.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

// ReSharper disable once CheckNamespace

namespace Proto;

public interface IContextStore
{
    /// <summary>
    ///     Gets a value from the actor context
    /// </summary>
    /// <typeparam name="T">The Type key of the value</typeparam>
    /// <returns>The value</returns>
    T? Get<T>();

    /// <summary>
    ///     Sets a value on the actor context
    /// </summary>
    /// <param name="obj">The value to set</param>
    /// <typeparam name="T">The Type key of the value</typeparam>
    void Set<T>(T obj) => Set<T, T>(obj);

    /// <summary>
    ///     Sets a value on the actor context
    /// </summary>
    /// <param name="obj">The value to set</param>
    /// <typeparam name="T">The Type key of the value</typeparam>
    /// <typeparam name="TI">Type of the value, if different from the Type key</typeparam>
    void Set<T, TI>(TI obj) where TI : T;

    /// <summary>
    ///     Removes a value from the actor context
    /// </summary>
    /// <typeparam name="T">The Type key of the value</typeparam>
    void Remove<T>();
}