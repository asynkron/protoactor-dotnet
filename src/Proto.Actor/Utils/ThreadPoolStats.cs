// -----------------------------------------------------------------------
// <copyright file="ThreadPoolStats.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Proto.Utils
{
    [PublicAPI]
    public static class ThreadPoolStats
    {
        public static async Task Run(TimeSpan interval, Action<TimeSpan> callback, CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(interval, cancellationToken);
                var t1 = DateTime.UtcNow;
                var t2 = await Task.Run(async () => {
                        await Task.Yield();
                        return DateTime.UtcNow;
                    }, cancellationToken
                );

                var delta = t2 - t1;
                callback(delta);
            }
        }
    }
}