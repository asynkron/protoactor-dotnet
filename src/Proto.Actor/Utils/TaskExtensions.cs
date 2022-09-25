// -----------------------------------------------------------------------
// <copyright file="TaskExtensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Utils;

public static class TaskExtensions
{
    /// <summary>
    ///     Adds a timeout to a task. If the task times out (or provided token is cancelled),
    ///     <see cref="TaskCanceledException" /> is thrown.
    /// </summary>
    /// <param name="task">Task to be awaited</param>
    /// <param name="timeout">Timeout</param>
    /// <param name="ct"></param>
    /// <typeparam name="TResult"></typeparam>
    /// <returns></returns>
    /// <exception cref="TimeoutException"></exception>
    public static async Task<TResult> WithTimeout<TResult>(this Task<TResult> task, TimeSpan timeout,
        CancellationToken? ct = null)
    {
        if (task.IsCompleted)
        {
            return await task.ConfigureAwait(false); // Very important in order to propagate exceptions
        }

        using var timeoutCancellationTokenSource =
            ct switch
            {
                not null => CancellationTokenSource.CreateLinkedTokenSource(ct.Value),
                _        => new CancellationTokenSource()
            };

        var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token))
            .ConfigureAwait(false);

        if (completedTask != task)
        {
            throw new TimeoutException("The operation has timed out.");
        }

        timeoutCancellationTokenSource.Cancel();

        return await task.ConfigureAwait(false); // Very important in order to propagate exceptions
    }

    /// <summary>
    ///     Waits up to given timeout (or provided token is cancelled), returns true if task completed, false otherwise
    /// </summary>
    public static async Task<bool> WaitUpTo(this Task task, TimeSpan timeout, CancellationToken? ct = null)
    {
        if (!task.IsCompleted)
        {
            using var timeoutCancellationTokenSource =
                ct is not null
                    ? CancellationTokenSource.CreateLinkedTokenSource(ct.Value)
                    : new CancellationTokenSource();

            var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token))
                .ConfigureAwait(false);

            if (completedTask != task)
            {
                return false;
            }

            timeoutCancellationTokenSource.Cancel();
        }

        await task.ConfigureAwait(false); // Very important in order to propagate exceptions

        return true;
    }

    /// <summary>
    ///     Waits up to given timeout (or provided token is cancelled), returns (true,value) if task completed, (false,
    ///     default) otherwise
    /// </summary>
    public static async Task<(bool completed, TResult result)> WaitUpTo<TResult>(
        this Task<TResult> task,
        TimeSpan timeout,
        CancellationToken? ct = null
    )
    {
        if (!task.IsCompleted)
        {
            using var timeoutCancellationTokenSource =
                ct is not null
                    ? CancellationTokenSource.CreateLinkedTokenSource(ct.Value)
                    : new CancellationTokenSource();

            var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token))
                .ConfigureAwait(false);

            if (completedTask != task)
            {
                return (false, default!);
            }

            timeoutCancellationTokenSource.Cancel();
        }

        var result = await task.ConfigureAwait(false); // Very important in order to propagate exceptions

        return (true, result);
    }
}