// -----------------------------------------------------------------------
// <copyright file="ConnectionThrottlingPipeline.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace Proto.Cluster.Identity.MongoDb
{
    public static class ConnectionThrottlingPipeline
    {
        private static SemaphoreSlim openConnectionSemaphore = null!;

        public static void Initialize(int maxConcurrencyLevel)
            => openConnectionSemaphore = new SemaphoreSlim(
                0,
                maxConcurrencyLevel
            );

        public static async Task<T> AddRequest<T>(Task<T> task)
        {

            await openConnectionSemaphore.WaitAsync();

            try
            {
                var result = await task;
                return result;
            }
            finally
            {
                openConnectionSemaphore.Release();
            }
        }

        public static async Task AddRequest(Task task)
        {
            await openConnectionSemaphore.WaitAsync();

            try
            {
                await task;
            }
            finally
            {
                openConnectionSemaphore.Release();
            }
        }
    }
}