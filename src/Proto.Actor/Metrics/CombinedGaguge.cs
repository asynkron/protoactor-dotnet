using System.Collections.Generic;

namespace Proto.Metrics
{
    public class CombinedGauge : IGaugeMetric
    {
        private readonly IEnumerable<IGaugeMetric> _inner;

        internal CombinedGauge(ICollection<IGaugeMetric> inner) => _inner = inner;

        public void Set(double value, params string[]? labels) => _inner.ForEach(x => x.Set(value, labels));
    }
}