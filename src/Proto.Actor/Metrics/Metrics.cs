using System;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Proto.Utils;
using Ubiquitous.Metrics;
using Ubiquitous.Metrics.Labels;

namespace Proto.Metrics
{
    [PublicAPI]
    public class ProtoMetrics
    {
        private TypeDictionary<object> _knownMetrics = new();
        private IMetricsProvider[] _configurators;
        private Ubiquitous.Metrics.Metrics _metrics;
        internal readonly ActorMetrics InternalActorMetrics;
        public readonly bool IsNoop;

        public ProtoMetrics(IMetricsProvider[] configurators)
        {
            if (configurators.FirstOrDefault() is NoMetricsProvider)
            {
                IsNoop = true;
            }
            
            _metrics = Ubiquitous.Metrics.Metrics.CreateUsing(configurators);
            _configurators = configurators;
            InternalActorMetrics = new ActorMetrics(this);
            RegisterKnownMetrics(InternalActorMetrics);
        }

        public void RegisterKnownMetrics<T>(T instance) => _knownMetrics.Add<T>(instance!);

        public T Get<T>() => (T)_knownMetrics.Get<T>()!;

        public ICountMetric CreateCount(string name, string description, params LabelName[] labelNames) => _metrics.CreateCount(name, description,labelNames);

        public IGaugeMetric CreateGauge(string name, string description, params LabelName[] labelNames) => _metrics.CreateGauge(name, description, labelNames);

        public IHistogramMetric CreateHistogram(string name, string description, params LabelName[] labelNames) => _metrics.CreateHistogram(name, description, labelNames);
    }
}