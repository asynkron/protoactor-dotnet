// -----------------------------------------------------------------------
// <copyright file="IKeyValueStore.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Proto.Utils;

/// <summary>
///     A key value store abstraction.
/// </summary>
/// <typeparam name="T"></typeparam>
[PublicAPI]
public interface IKeyValueStore<T>
{
    /// <summary>
    ///     Get the value for the given key.
    /// </summary>
    /// <param name="id">Key</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<T> GetAsync(string id, CancellationToken ct);

    /// <summary>
    ///     Set the value for the given key.
    /// </summary>
    /// <param name="id">Key</param>
    /// <param name="state">Value</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task SetAsync(string id, T state, CancellationToken ct);

    /// <summary>
    ///     Clear the value for the given key.
    /// </summary>
    /// <param name="id">Key</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task ClearAsync(string id, CancellationToken ct);
}