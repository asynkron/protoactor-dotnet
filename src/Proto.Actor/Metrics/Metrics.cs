using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Proto.Metrics
{
    [PublicAPI]
    public class Metrics
    {
        IConfigureMetrics[]? _configurators;

        static Metrics() => Instance = new Metrics();

        /// <summary>
        /// Get the Metrics instance. Normally, you'd need only one Metrics instance per application.
        /// </summary>
        public static Metrics Instance { get; }

        public static Metrics CreateUsing(params IConfigureMetrics[] configurators) {
            Instance._configurators = configurators;

            return Instance;
        }

        public ICountMetric CreateCount(string name, string[] labelNames) {
            EnsureConfigured();
            return new CombinedCount(_configurators.Select(x => x.CreateCount(name, labelNames)).ToList());
        }

        public IHistogramMetric CreateHistogram(string name, string[] labelNames) {
            EnsureConfigured();
            return new CombinedHistogram(_configurators.Select(x => x.CreateHistogram(name, labelNames)).ToList());
        }

        public IGaugeMetric CreateGauge(string name, string[] labelNames) {
            EnsureConfigured();
            return new CombinedGauge(_configurators.Select(x => x.CreateGauge(name, labelNames)).ToList());
        }

        public static async Task Measure(
            Func<Task> action,
            IHistogramMetric metric,
            ICountMetric? errorCount = null,
            int count = 1,
            string[]? labels = null
        ) {
            var stopwatch = Stopwatch.StartNew();

            try {
                await action();
            }
            catch (Exception) {
                errorCount?.Inc(labels: labels);

                throw;
            }
            finally {
                stopwatch.Stop();
                metric.Observe(stopwatch, labels, count);
            }
        }

        public static void Measure(
            Action action,
            IHistogramMetric metric,
            ICountMetric? errorCount = null,
            int count = 1,
            string[]? labels = null
        ) {
            var stopwatch = Stopwatch.StartNew();

            try {
                action();
            }
            catch (Exception) {
                errorCount?.Inc(labels: labels);

                throw;
            }
            finally {
                stopwatch.Stop();
                metric.Observe(stopwatch, labels, count);
            }
        }

        public static async Task<T> Measure<T>(
            Func<Task<T>> action,
            IHistogramMetric metric,
            ICountMetric? errorCount = null,
            int count = 1,
            string[]? labels = null
        ) {
            var stopwatch = Stopwatch.StartNew();

            T result;

            try {
                result = await action();
            }
            catch (Exception) {
                errorCount?.Inc(labels: labels);

                throw;
            }
            finally {
                stopwatch.Stop();
                metric.Observe(stopwatch, labels, count);
            }

            return result;
        }

        void EnsureConfigured() {
            if (_configurators == null)
                throw new InvalidOperationException("Metrics instance is not configured");
        }
    }
}
