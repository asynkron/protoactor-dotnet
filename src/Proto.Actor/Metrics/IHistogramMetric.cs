using System;
using System.Diagnostics;

namespace Proto.Metrics
{
    public interface IHistogramMetric
    {
        void Observe(Stopwatch stopwatch, string[]? labels = null, int count = 1);

        void Observe(DateTimeOffset when, string[]? labels = null);
    }
}