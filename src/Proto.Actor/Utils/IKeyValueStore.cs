// -----------------------------------------------------------------------
// <copyright file="IKeyValueStore.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Proto.Utils
{
    [PublicAPI]
    public interface IKeyValueStore<T>
    {
        Task<T> GetAsync(string id, CancellationToken ct);

        Task SetAsync(string id, T state, CancellationToken ct);

        Task ClearAsync(string id, CancellationToken ct);

    }
}