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
        private IConfigureMetrics[] _configurators;


        public Metrics(IConfigureMetrics[] configurators)
        {
            _configurators = configurators;
        }
        // public static Metrics CreateUsing(params IConfigureMetrics[] configurators)
        // {
        //     Instance._configurators = configurators;
        //
        //     return Instance;
        // }

        public ICountMetric CreateCount(string name, string[] labelNames) => new CombinedCount(_configurators.Select(x => x.CreateCount(name, labelNames)).ToList());

        public IHistogramMetric CreateHistogram(string name, string[] labelNames) => new CombinedHistogram(_configurators.Select(x => x.CreateHistogram(name, labelNames)).ToList());

        public IGaugeMetric CreateGauge(string name, string[] labelNames) => new CombinedGauge(_configurators.Select(x => x.CreateGauge(name, labelNames)).ToList());

        public async Task Measure(
            Func<Task> action,
            IHistogramMetric metric,
            ICountMetric? errorCount = null,
            int count = 1,
            string[]? labels = null
        )
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await action();
            }
            catch (Exception)
            {
                errorCount?.Inc(labels: labels);

                throw;
            }
            finally
            {
                stopwatch.Stop();
                metric.Observe(stopwatch, labels, count);
            }
        }

        public void Measure(
            Action action,
            IHistogramMetric metric,
            ICountMetric? errorCount = null,
            int count = 1,
            string[]? labels = null
        )
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                action();
            }
            catch (Exception)
            {
                errorCount?.Inc(labels: labels);

                throw;
            }
            finally
            {
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
        )
        {
            var stopwatch = Stopwatch.StartNew();

            T result;

            try
            {
                result = await action();
            }
            catch (Exception)
            {
                errorCount?.Inc(labels: labels);

                throw;
            }
            finally
            {
                stopwatch.Stop();
                metric.Observe(stopwatch, labels, count);
            }

            return result;
        }

        private void EnsureConfigured()
        {
            if (_configurators == null)
                throw new InvalidOperationException("Metrics instance is not configured");
        }
    }
}