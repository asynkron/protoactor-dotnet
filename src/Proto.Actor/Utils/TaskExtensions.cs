// -----------------------------------------------------------------------
// <copyright file="TaskExtensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
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

}