// -----------------------------------------------------------------------
// <copyright file="ConnectionThrottlingPipeline.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Utils
{
    public class AsyncSemaphore
    {
        private readonly SemaphoreSlim _semaphore;

        public AsyncSemaphore(int maxConcurrency) => _semaphore = new SemaphoreSlim(
            maxConcurrency,
            maxConcurrency
        );

        public async Task<T> WaitAsync<T>(Func<Task<T>> producer)
        {
            await _semaphore.WaitAsync();

            try
            {
                var task = producer();
                var result = await task;
                return result;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task WaitAsync(Func<Task> producer)
        {
            await _semaphore.WaitAsync();

            try
            {
                var task = producer();
                await task;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Wait(Func<Task> producer)
        {
            //block caller
            _semaphore.Wait();

            _ = SafeTask.Run(async () => {
                    try
                    {
                        var task = producer();
                        await task;
                    }
                    finally
                    {
                        //release once the async flow is done
                        _semaphore.Release();
                    }
                }
            );
        }
    }
}