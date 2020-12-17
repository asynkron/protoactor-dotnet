// -----------------------------------------------------------------------
// <copyright file="ConnectionThrottlingPipeline.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Interactive
{
    public class AsyncSemaphore
    {
        private readonly SemaphoreSlim _semaphore;

        public AsyncSemaphore(int maxConcurrency)
        {
            _semaphore = new SemaphoreSlim(
                maxConcurrency,
                maxConcurrency
            );
        }

        public async Task<T> WaitAsync<T>(Task<T> task)
        {

            await _semaphore.WaitAsync();

            try
            {
                var result = await task;
                return result;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task WaitAsync(Task task)
        {
            await _semaphore.WaitAsync();

            try
            {
                await task;
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}