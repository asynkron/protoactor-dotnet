// -----------------------------------------------------------------------
// <copyright file="ConnectionThrottlingPipeline.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Utils;

/// <summary>
///     AsyncSemaphore allows to limit the number of concurrent tasks to a maximum number.
/// </summary>
public class AsyncSemaphore
{
    private readonly SemaphoreSlim _semaphore;

    /// <summary>
    ///     Creates a new instance of <see cref="AsyncSemaphore" />.
    /// </summary>
    /// <param name="maxConcurrency">Maximum number of concurrent tasks</param>
    public AsyncSemaphore(int maxConcurrency)
    {
        _semaphore = new SemaphoreSlim(
            maxConcurrency,
            maxConcurrency
        );
    }

    /// <summary>
    ///     Starts and awaits a task when a slot within the maximum number of concurrent tasks is available.
    /// </summary>
    /// <param name="producer">Delegate to start the task</param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public async Task<T> WaitAsync<T>(Func<Task<T>> producer)
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);

        try
        {
            var task = producer();
            var result = await task.ConfigureAwait(false);

            return result;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    ///     Starts and awaits a task when a slot within the maximum number of concurrent tasks is available.
    /// </summary>
    /// <param name="producer">Delegate to start the task</param>
    public async Task WaitAsync(Func<Task> producer)
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);

        try
        {
            var task = producer();
            await task.ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    ///     Starts a task when a slot within the maximum number of concurrent tasks is available. The caller will be blocked
    ///     until the slot is available,
    ///     however then the task is run asynchronously and the method returns.
    /// </summary>
    /// <param name="producer">Delegate to start the task</param>
    public void Wait(Func<Task> producer)
    {
        //block caller
        _semaphore.Wait();

        _ = SafeTask.Run(async () =>
            {
                try
                {
                    var task = producer();
                    await task.ConfigureAwait(false);
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