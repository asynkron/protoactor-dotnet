// ReSharper disable NotAccessedField.Local

using System;
using System.Diagnostics;

namespace Proto.Metrics
{
    public class NoMetrics : IConfigureMetrics
    {
        public ICountMetric CreateCount(string name, string[] labelNames)
            => new NoCount(name, labelNames);

        public IHistogramMetric CreateHistogram(string name, string[] labelNames)
            => new NoHistogram(name, labelNames);

        public IGaugeMetric CreateGauge(string name, string[] labelNames)
            => new NoGauge(name, labelNames);

        class NoMetric
        {
            readonly string[] _labelNames;
            readonly string   _name;

            protected NoMetric(string name, string[] labelNames) {
                _name       = name;
                _labelNames = labelNames;
            }
        }

        class NoCount : NoMetric, ICountMetric
        {
            int _count;

            protected internal NoCount(string name, string[] labelNames) : base(name, labelNames) { }

            public void Inc(int count = 1, params string[]? labels) => _count += count;
        }

        class NoHistogram : NoMetric, IHistogramMetric
        {
            protected internal NoHistogram(string name, string[] labelNames) : base(name, labelNames) { }

            public void Observe(Stopwatch stopwatch, string[]? labels = null, int count = 1) => Observe(stopwatch.ElapsedMilliseconds * 1000, labels);

            public void Observe(DateTimeOffset when, string[]? labels = null) => Observe((DateTimeOffset.Now - when).Seconds, labels);

            void Observe(double seconds, string[]? labels) { }
        }

        class NoGauge : NoMetric, IGaugeMetric
        {
            protected internal NoGauge(string name, string[] labelNames) : base(name, labelNames) { }

            public void Set(double value, params string[]? labels) { }
        }
    }
}
