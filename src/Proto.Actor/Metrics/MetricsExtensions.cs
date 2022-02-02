// -----------------------------------------------------------------------
// <copyright file="MetricsExtensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Proto
{
    [PublicAPI]
    public static class MetricsExtensions
    {
        public static async Task<T> Observe<T>(this Histogram<double> histogram, Func<Task<T>> factory, params KeyValuePair<string, object?>[] tags)
        {
            var sw = Stopwatch.StartNew();
            var t = factory();
            var res = await t;
            sw.Stop();

            histogram.Record(sw.Elapsed.TotalSeconds, tags);

            return res;
        }

        public static async Task Observe(this Histogram<double> histogram, Func<Task> factory, params KeyValuePair<string, object?>[] tags)
        {
            var sw = Stopwatch.StartNew();
            var t = factory();
            await t;
            sw.Stop();
            histogram.Record(sw.Elapsed.TotalSeconds, tags);
        }
    }
}