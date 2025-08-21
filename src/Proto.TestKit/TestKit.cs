// -----------------------------------------------------------------------
// <copyright file="TestKit.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.TestKit;

/// <summary>
/// Helper methods for writing tests against Proto.Actor components.
/// </summary>
public static class TestKit
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(20);

    /// <summary>
    /// Repeatedly evaluates <paramref name="condition"/> until it returns <c>true</c> or
    /// the <paramref name="timeout"/> elapses.
    /// </summary>
    /// <param name="condition">Condition to evaluate.</param>
    /// <param name="timeout">Maximum time to wait for the condition.</param>
    /// <param name="message">Optional message included in the thrown <see cref="TimeoutException"/>.</param>
    /// <exception cref="TimeoutException">Thrown when the condition does not become true within the timeout.</exception>
    public static Task AwaitConditionAsync(Func<bool> condition, TimeSpan timeout, string? message = null) =>
        AwaitConditionAsync(() => Task.FromResult(condition()), timeout, CancellationToken.None, message);

    /// <summary>
    /// Repeatedly evaluates <paramref name="condition"/> until it returns <c>true</c> or
    /// the <paramref name="timeout"/> elapses.
    /// </summary>
    /// <param name="condition">Condition to evaluate asynchronously.</param>
    /// <param name="timeout">Maximum time to wait for the condition.</param>
    /// <param name="message">Optional message included in the thrown <see cref="TimeoutException"/>.</param>
    /// <exception cref="TimeoutException">Thrown when the condition does not become true within the timeout.</exception>
    public static Task AwaitConditionAsync(Func<Task<bool>> condition, TimeSpan timeout, string? message = null) =>
        AwaitConditionAsync(condition, timeout, CancellationToken.None, message);

    /// <summary>
    /// Repeatedly evaluates <paramref name="condition"/> until it returns <c>true</c>,
    /// the <paramref name="timeout"/> elapses or the <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    /// <param name="condition">Condition to evaluate asynchronously.</param>
    /// <param name="timeout">Maximum time to wait for the condition.</param>
    /// <param name="cancellationToken">Token used to cancel the wait.</param>
    /// <param name="message">Optional message included in the thrown <see cref="TimeoutException"/>.</param>
    /// <exception cref="TimeoutException">Thrown when the condition does not become true within the timeout.</exception>
    public static async Task AwaitConditionAsync(Func<Task<bool>> condition, TimeSpan timeout,
        CancellationToken cancellationToken, string? message = null)
    {
        var stop = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < stop)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await condition().ConfigureAwait(false))
            {
                return;
            }

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException(message ?? $"The condition was not met within the timeout of {timeout}");
    }
}

