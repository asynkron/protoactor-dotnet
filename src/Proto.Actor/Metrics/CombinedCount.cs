using System.Collections.Generic;

namespace Proto.Metrics
{
    public class CombinedCount : ICountMetric
    {
        private readonly ICollection<ICountMetric> _inner;

        internal CombinedCount(ICollection<ICountMetric> inner) => _inner = inner;

        public void Inc(int count = 1, params string[]? labels) => _inner.ForEach(x => x.Inc(count, labels));
    }
}