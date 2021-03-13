// -----------------------------------------------------------------------
// <copyright file="ThreadPoolStats.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;

namespace Proto.Utils
{
    public static class ThreadPoolStats
    {
        public static async Task Run(TimeSpan interval, TimeSpan limit, Action<TimeSpan> callback)
        {
            await Task.Yield();

            while (true)
            {
                await Task.Delay(interval);
                var t1 = DateTime.UtcNow;
                var t2 = await Task.Run(async () => {
                        await Task.Yield();
                        return DateTime.UtcNow;
                    }
                );

                var delta = t2 - t1;
                if (delta > limit)
                    callback(delta);
            }
        }
    }
}