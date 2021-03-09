using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Proto.Utils;
using Ubiquitous.Metrics;
using Ubiquitous.Metrics.Combined;

namespace Proto.Metrics
{
    [PublicAPI]
    public class Metrics : Ubiquitous.Metrics.Metrics
    {
        

        private TypeDictionary<object> _knownMetrics = new();
        private IMetricsProvider[] _configurators;

        public Metrics(IMetricsProvider[] configurators)
        {
            _configurators = configurators;
            RegisterKnownMetrics(new ActorMetrics(this));
        }

        public void RegisterKnownMetrics<T>(T instance) => _knownMetrics.Add<T>(instance!);

        public T Get<T>() => (T)_knownMetrics.Get<T>()!;
    }
}