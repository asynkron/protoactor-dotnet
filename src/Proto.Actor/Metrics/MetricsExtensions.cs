// -----------------------------------------------------------------------
// <copyright file="MetricsExtensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Ubiquitous.Metrics;

namespace Proto
{
    [PublicAPI]
    public static class MetricsExtensions
    {
        public static async Task<T> Observe<T>(this IHistogramMetric histogram, Func<Task<T>> factory, params string[] labels)
        {
            var sw = Stopwatch.StartNew();
            var t = factory();
            var res = await t;
            sw.Stop();
            histogram.Observe(sw, labels);

            return res;
        }
        
        public static async Task Observe(this IHistogramMetric histogram, Func<Task> factory, params string[] labels)
        {
            var sw = Stopwatch.StartNew();
            var t = factory();
            await t;
            sw.Stop();
            histogram.Observe(sw, labels);
        }
    }
}