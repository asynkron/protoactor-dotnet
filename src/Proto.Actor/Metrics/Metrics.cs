using System.Linq;
using JetBrains.Annotations;
using Proto.Utils;
using Ubiquitous.Metrics;
using Ubiquitous.Metrics.Labels;
using Ubiquitous.Metrics.NoMetrics;

namespace Proto.Metrics
{
    [PublicAPI]
    public class ProtoMetrics
    {
        internal readonly ActorMetrics InternalActorMetrics;
        public readonly bool IsNoop;
        private IMetricsProvider[] _configurators;
        private TypeDictionary<object,ProtoMetrics> _knownMetrics = new(10);
        private Ubiquitous.Metrics.Metrics _metrics;

        public ProtoMetrics(IMetricsProvider[] configurators)
        {
            if (configurators.FirstOrDefault() is NoMetricsProvider) IsNoop = true;

            _metrics = Ubiquitous.Metrics.Metrics.CreateUsing(configurators);
            _configurators = configurators;
            InternalActorMetrics = new ActorMetrics(this);
            Register(InternalActorMetrics);
        }

        public void Register<T>(T instance) => _knownMetrics.Add<T>(instance!);

        public T Get<T>() => (T) _knownMetrics.Get<T>()!;

        public ICountMetric CreateCount(string name, string description, params LabelName[] labelNames)
            => _metrics.CreateCount(name, description, labelNames);

        public IGaugeMetric CreateGauge(string name, string description, params LabelName[] labelNames)
            => _metrics.CreateGauge(name, description, labelNames);

        public IHistogramMetric CreateHistogram(string name, string description, params LabelName[] labelNames)
            => _metrics.CreateHistogram(name, description, labelNames);
    }
}