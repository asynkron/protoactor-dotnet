// -----------------------------------------------------------------------
// <copyright file="Retry.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;

namespace Proto.Utils
{
    public static class Retry
    {
        public class RetriesExhaustedException : Exception
        {
            public RetriesExhaustedException(string message): base(message){}

            public RetriesExhaustedException(string message, Exception innerException) : base(message, innerException){}
        }

        public const int Forever = 0;

        public static Task<T> TryUntilNotNull<T>(
            Func<Task<T>> body,
            int retryCount = 10,
            int backoffMilliSeconds = 100,
            int maxBackoffMilliseconds = 5000,
            Action<int, Exception>? onError = null,
            Action<Exception>? onFailed = null
        ) where T : class => TryUntil(body, res => res != null, retryCount, backoffMilliSeconds, maxBackoffMilliseconds, onError, onFailed);

        public static async Task<T> TryUntil<T>(Func<Task<T>> body, Func<T?,bool> condition, int retryCount = 10, int backoffMilliSeconds = 100, int maxBackoffMilliseconds = 5000, Action<int,Exception>? onError=null, Action<Exception>? onFailed=null)
        {
            for (var i = 0; retryCount == 0 || i < retryCount; i++)
            {
                try
                {
                    var res = await body();

                    if (condition(res))
                    {
                        return res;
                    }
                }
                catch (Exception x)
                {
                    onError?.Invoke(i, x);

                    if (i == retryCount - 1)
                    {
                        onFailed?.Invoke(x);
                        throw new RetriesExhaustedException("Retried but failed", x);
                    }

                    var backoff = Math.Min(i * backoffMilliSeconds, maxBackoffMilliseconds);
                    await Task.Delay(backoff);
                }
            }

            throw new RetriesExhaustedException("Retry condition was never met");
        }
        
        public static async Task<T> Try<T>(Func<Task<T>> body, int retryCount = 10, int backoffMilliSeconds = 100, int maxBackoffMilliseconds = 5000, Action<int,Exception>? onError=null, Action<Exception>? onFailed=null)
        {
            for (var i = 0; retryCount == 0 || i < retryCount; i++)
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
                        throw new RetriesExhaustedException("Retried but failed", x);
                    }

                    var backoff = Math.Min(i * backoffMilliSeconds, maxBackoffMilliseconds);
                    await Task.Delay(backoff);
                }
            }

            throw new RetriesExhaustedException("This should never happen...");
        }

        public static async Task Try(
            Func<Task> body,
            int retryCount = 10,
            int backoffMilliSeconds = 100,
            int maxBackoffMilliseconds = 5000,
            Action<int, Exception>? onError = null,
            Action<Exception>? onFailed = null,
            bool ignoreFailure = false
        )
        {
            for (var i = 0; retryCount == 0 || i < retryCount; i++)
            {
                try
                {
                    await body();
                    return;
                }
                catch (Exception x)
                {
                    onError?.Invoke(i, x);

                    if (i == retryCount - 1)
                    {
                        onFailed?.Invoke(x);

                        if (ignoreFailure)
                            return;

                        throw new RetriesExhaustedException("Retried but failed", x);
                    }

                    var backoff = Math.Min(i * backoffMilliSeconds, maxBackoffMilliseconds);
                    await Task.Delay(backoff);
                }
            }

            throw new RetriesExhaustedException("This should never happen...");
        }
    }
}