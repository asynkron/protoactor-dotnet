using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using JetBrains.Annotations;
using Proto.Utils;

namespace Proto.Metrics
{
    [PublicAPI]
    public class ProtoMetrics
    {
        public const string MeterName = "Proto.Actor";

        internal static Meter Meter = new(MeterName, typeof(ProtoMetrics).Assembly.GetName().Version?.ToString());

        internal readonly ActorMetrics InternalActorMetrics;
        public readonly bool IsNoop;
        private TypeDictionary<object, ProtoMetrics> _knownMetrics = new(10);

        public ProtoMetrics(bool recordMetrics)
        {
            InternalActorMetrics = new ActorMetrics(this);
            Register(InternalActorMetrics);
            IsNoop = !recordMetrics;
        }

        public void Register<T>(T instance) => _knownMetrics.Add<T>(instance!);

        public T Get<T>() => (T) _knownMetrics.Get<T>()!;

        public Counter<T> CreateCounter<T>(string name, string? unit = null, string? description = null)
            where T : struct
            => Meter.CreateCounter<T>(name, unit, description);

        public Histogram<T> CreateHistogram<T>(string name, string? unit = null, string? description = null)
            where T : struct
            => Meter.CreateHistogram<T>(name, unit, description);

        public ObservableGauge<T> CreateObservableGauge<T>(string name, Func<IEnumerable<Measurement<T>>> observeValues, string? unit = null, string? description = null)
            where T : struct
            => Meter.CreateObservableGauge(name, observeValues, unit, description);
    }
}