// -----------------------------------------------------------------------
// <copyright file="TaskExtensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto.Utils
{
    public static class TaskExtensions
    {
        public static async Task<TResult> WithTimeout<TResult>(this Task<TResult> task, TimeSpan timeout)
        {
            using var timeoutCancellationTokenSource = new CancellationTokenSource();

            var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token));
            if (completedTask != task) throw new TimeoutException("The operation has timed out.");

            timeoutCancellationTokenSource.Cancel();
            return await task; // Very important in order to propagate exceptions
        }

       
    }

    public static class Retry
    {
        public static async Task<T> Try<T>(Func<Task<T>> body, int retryCount = 10, int backoffMilliSeconds = 100, Action<int,Exception>? onError=null, Action<Exception>? onFailed=null)
        {
            for (var i = 0; i < retryCount; i++)
            {
                try
                {
                    var res = await body();
                    return res;
                }
                catch(Exception x)
                {
                    onError?.Invoke(i,x);
                    
                    if (i == retryCount - 1)
                    {
                        onFailed?.Invoke(x);
                        throw;
                    }

                    await Task.Delay(i * backoffMilliSeconds);
                }
            }

            throw new Exception("This should never happen...");
        }
        
        public static async Task Try(Func<Task> body, int retryCount = 10, int backoffMilliSeconds = 100, Action<int,Exception>? onError=null, Action<Exception>? onFailed=null, bool ignoreFailure=false)
        {
            for (var i = 0; i < retryCount; i++)
            {
                try
                {
                    await body();
                    return;
                }
                catch(Exception x)
                {
                    onError?.Invoke(i,x);
                    
                    if (i == retryCount - 1)
                    {
                        onFailed?.Invoke(x);

                        if (ignoreFailure)
                            return;

                        throw;
                    }

                    await Task.Delay(i * backoffMilliSeconds);
                }
            }

            throw new Exception("This should never happen...");
        }
    }
}