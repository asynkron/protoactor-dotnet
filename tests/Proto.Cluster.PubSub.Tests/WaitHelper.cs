// -----------------------------------------------------------------------
// <copyright file = "WaitHelper.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

namespace Proto.Cluster.PubSub.Tests;

public static class WaitHelper
{
    public static Task WaitUntil(Func<bool> condition, string? errorMessage = null, TimeSpan? timeout = null)
        => WaitUntil(() => Task.FromResult(condition()), errorMessage, timeout);

    public static async Task WaitUntil(Func<Task<bool>> condition, string? errorMessage = null,
        TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(15);
        errorMessage ??= "The condition was not met within the timeout";
        using var cts = new CancellationTokenSource(timeout.Value);

        while (!cts.Token.IsCancellationRequested)
        {
            if (await condition())
            {
                return;
            }

            // ReSharper disable once MethodSupportsCancellation
            await Task.Delay(100);
        }

        throw new Exception($"Timeout after {timeout.Value}. {errorMessage}");
    }
}