using System.Diagnostics.Metrics;
using JetBrains.Annotations;

namespace Proto.Metrics
{
    [PublicAPI]
    public class ProtoMetrics
    {
        public const string MeterName = "Proto.Actor";

        public static Meter Meter = new(MeterName, typeof(ProtoMetrics).Assembly.GetName().Version?.ToString());

        public readonly bool Enabled;

        public ProtoMetrics(bool enabled) => Enabled = enabled;
    }
}