// -----------------------------------------------------------------------
// <copyright file="ConcurrentKeyValueStore.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Proto.Utils;

/// <summary>
///     A base class for a key value store, that limits the number of concurrent operations
/// </summary>
/// <typeparam name="T"></typeparam>
[PublicAPI]
public abstract class ConcurrentKeyValueStore<T> : IKeyValueStore<T>
{
    private readonly AsyncSemaphore _semaphore;

    /// <summary>
    ///     Creates a new instance of <see cref="ConcurrentKeyValueStore{T}" />
    /// </summary>
    /// <param name="semaphore"><see cref="AsyncSemaphore" /> that defines the concurrency limits</param>
    protected ConcurrentKeyValueStore(AsyncSemaphore semaphore)
    {
        _semaphore = semaphore;
    }

    public Task<T> GetAsync(string id, CancellationToken ct) => _semaphore.WaitAsync(() => InnerGetStateAsync(id, ct));

    public Task SetAsync(string id, T state, CancellationToken ct) =>
        _semaphore.WaitAsync(() => InnerSetStateAsync(id, state, ct));

    public Task ClearAsync(string id, CancellationToken ct) => _semaphore.WaitAsync(() => InnerClearStateAsync(id, ct));

    protected abstract Task<T> InnerGetStateAsync(string id, CancellationToken ct);

    protected abstract Task InnerSetStateAsync(string id, T state, CancellationToken ct);

    protected abstract Task InnerClearStateAsync(string id, CancellationToken ct);
}