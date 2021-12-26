// -----------------------------------------------------------------------
// <copyright file="TaskExtensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Utils
{
    public static class TaskExtensions
    {
        public static async Task<TResult> WithTimeout<TResult>(this Task<TResult> task, TimeSpan timeout, CancellationToken? ct = null)
        {
            using var timeoutCancellationTokenSource = ct is not null ? CancellationTokenSource.CreateLinkedTokenSource(ct.Value) : new CancellationTokenSource();

            var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token)).ConfigureAwait(false);
            if (completedTask != task) throw new TimeoutException("The operation has timed out.");

            timeoutCancellationTokenSource.Cancel();
            return await task; // Very important in order to propagate exceptions
        }

        /// <summary>
        /// Waits up to given timeout, returns (true,value) if task completed, (false, default) if it timed out
        /// </summary>
        public static async Task<(bool, TResult)> WaitUpTo<TResult>(this Task<TResult> task, TimeSpan timeout, CancellationToken? ct = null)
        {
            using var timeoutCancellationTokenSource = ct is not null ? CancellationTokenSource.CreateLinkedTokenSource(ct.Value) : new CancellationTokenSource();

            var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token)).ConfigureAwait(false);
            if (completedTask != task) return (false, default);

            timeoutCancellationTokenSource.Cancel();
            return (true, await task); // Very important in order to propagate exceptions
        }
       
    }
}