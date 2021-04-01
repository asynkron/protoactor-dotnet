// -----------------------------------------------------------------------
// <copyright file="ConcurrentKeyValueStore.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Proto.Utils
{
    namespace Proto.Utils
    {
        [PublicAPI]
        public abstract class ConcurrentKeyValueStore<T>
        {
            private readonly AsyncSemaphore _semaphore;

            protected ConcurrentKeyValueStore(AsyncSemaphore semaphore) => _semaphore = semaphore;

            public Task<T?> GetStateAsync(string id) => _semaphore.WaitAsync(() => InnerGetStateAsync(id));
            public Task SetStateAsync(string id, T state) => _semaphore.WaitAsync(() => InnerSetStateAsync(id, state));
            public Task ClearStateAsync(string id) => _semaphore.WaitAsync(() => InnerClearStateAsync(id));

            protected abstract Task<T?> InnerGetStateAsync(string id);
            protected abstract Task InnerSetStateAsync(string id, T state);
            protected abstract Task InnerClearStateAsync(string id);
        }
    }
}
