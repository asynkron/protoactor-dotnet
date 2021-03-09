using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Proto.Metrics
{
    public class CombinedHistogram : IHistogramMetric
    {
        private readonly ICollection<IHistogramMetric> _inner;

        internal CombinedHistogram(ICollection<IHistogramMetric> inner) => _inner = inner;

        public void Observe(Stopwatch stopwatch, string[]? labels = null, int count = 1) => _inner.ForEach(x => x.Observe(stopwatch, labels, count));

        public void Observe(DateTimeOffset when, string[]? labels = null) => _inner.ForEach(x => x.Observe(when, labels));
    }
}