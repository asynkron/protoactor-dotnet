// -----------------------------------------------------------------------
// <copyright file = "WaitHelper.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Proto.Cluster.PubSub.Tests;

public static class WaitHelper
{
    public static async Task WaitUntil(Func<bool> condition, TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(5);
        
        var cts = new CancellationTokenSource(timeout.Value);

        while (!cts.Token.IsCancellationRequested)
        {
            if (condition()) return;

            await Task.Delay(100);
        }
        
        throw new Exception($"The condition was not met within the timeout of {timeout.Value}");
    }
}