// -----------------------------------------------------------------------
// <copyright file="ConnectionThrottlingPipeline.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Interactive
{
    public class Throttler
    {
        private SemaphoreSlim concurrencySemaphore;

        public Throttler(int maxConcurrency)
        {
            concurrencySemaphore = new SemaphoreSlim(
                maxConcurrency,
                maxConcurrency
            );
        }

        public async Task<T> AddRequest<T>(Task<T> task)
        {

            await concurrencySemaphore.WaitAsync();

            try
            {
                var result = await task;
                return result;
            }
            finally
            {
                concurrencySemaphore.Release();
            }
        }

        public async Task AddRequest(Task task)
        {
            await concurrencySemaphore.WaitAsync();

            try
            {
                await task;
            }
            finally
            {
                concurrencySemaphore.Release();
            }
        }
    }
}