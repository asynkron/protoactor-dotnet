// -----------------------------------------------------------------------
// <copyright file="ThreadPoolStats.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Proto.Utils;

[PublicAPI]
public static class ThreadPoolStats
{
    public static async Task Run(TimeSpan interval, Action<TimeSpan> callback,
        CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        while (!cancellationToken.IsCancellationRequested)
        {
            // Wait for the configured sampling interval before measuring again
            await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            var startTime = DateTime.UtcNow;

            var endTime = await Task.Run(async () =>
                {
                    await Task.Yield();

                    return DateTime.UtcNow;
                }, cancellationToken
            ).ConfigureAwait(false);

            var delta = endTime - startTime;
            callback(delta);
        }
    }
}