// -----------------------------------------------------------------------
// <copyright file="Retry.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Utils;

public static class Retry
{
    public const int Forever = 0;

    public static Task<T> TryUntilNotNull<T>(
        Func<Task<T>> body,
        int retryCount = 10,
        int backoffMilliSeconds = 100,
        int maxBackoffMilliseconds = 5000,
        Action<int, Exception>? onError = null,
        Action<Exception>? onFailed = null,
        CancellationToken ct = default
    ) where T : class =>
        TryUntil(body, res => res != null, retryCount, backoffMilliSeconds, maxBackoffMilliseconds, onError,
            onFailed, ct);

    public static Task<T> TryUntil<T>(Func<Task<T>> body, Func<T?, bool> condition, int retryCount = 10,
        int backoffMilliSeconds = 100, int maxBackoffMilliseconds = 5000, Action<int, Exception>? onError = null,
        Action<Exception>? onFailed = null, CancellationToken ct = default) =>
        TryInternal(body, condition, retryCount, backoffMilliSeconds, maxBackoffMilliseconds, onError, onFailed,
            ignoreFailure: false, checkFailFast: true, ct);

    public static Task<T> Try<T>(Func<Task<T>> body, int retryCount = 10, int backoffMilliSeconds = 100,
        int maxBackoffMilliseconds = 5000, Action<int, Exception>? onError = null, Action<Exception>? onFailed = null,
        CancellationToken ct = default) =>
        TryInternal(body, condition: null, retryCount, backoffMilliSeconds, maxBackoffMilliseconds, onError, onFailed,
            ignoreFailure: false, checkFailFast: false, ct);

    public static async Task Try(
        Func<Task> body,
        int retryCount = 10,
        int backoffMilliSeconds = 100,
        int maxBackoffMilliseconds = 5000,
        Action<int, Exception>? onError = null,
        Action<Exception>? onFailed = null,
        bool ignoreFailure = false,
        CancellationToken ct = default
    ) =>
        await TryInternal(
                async () =>
                {
                    await body().ConfigureAwait(false);

                    return true;
                },
                _ => true,
                retryCount,
                backoffMilliSeconds,
                maxBackoffMilliseconds,
                onError,
                onFailed,
                ignoreFailure,
                checkFailFast: true,
                ct
            )
            .ConfigureAwait(false);

    private static async Task<T> TryInternal<T>(
        Func<Task<T>> body,
        Func<T?, bool>? condition,
        int retryCount,
        int backoffMilliSeconds,
        int maxBackoffMilliseconds,
        Action<int, Exception>? onError,
        Action<Exception>? onFailed,
        bool ignoreFailure,
        bool checkFailFast,
        CancellationToken ct
    )
    {
        for (var i = 0; retryCount == 0 || i < retryCount; i++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var res = await body().ConfigureAwait(false);

                if (condition == null || condition(res))
                {
                    return res!;
                }
            }
            catch (Exception x)
            {
                if (checkFailFast)
                {
                    x.CheckFailFast();
                }

                onError?.Invoke(i, x);

                if (i == retryCount - 1)
                {
                    onFailed?.Invoke(x);

                    if (ignoreFailure)
                    {
                        return default!;
                    }

                    throw new RetriesExhaustedException("Retried but failed", x);
                }
            }

            var backoff = Math.Min((i + 1) * backoffMilliSeconds, maxBackoffMilliseconds);
            // Linear backoff: increase delay per attempt but cap to avoid unbounded waiting
            await Task.Delay(backoff, ct).ConfigureAwait(false);
        }

        if (ignoreFailure)
        {
            return default!;
        }

        throw new RetriesExhaustedException(
            condition == null ? "This should never happen..." : "Retry condition was never met");
    }

#pragma warning disable RCS1194
    public class RetriesExhaustedException : Exception
#pragma warning restore RCS1194
    {
        public RetriesExhaustedException(string message) : base(message)
        {
        }

        public RetriesExhaustedException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}