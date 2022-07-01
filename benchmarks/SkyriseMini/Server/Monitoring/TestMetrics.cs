using System.Diagnostics.Metrics;
using OpenTelemetry.Metrics;

namespace SkyriseMini.Monitoring;

public static class TestMetrics
{
    public const string MeterName = "TestRunner";

    public static Meter TestMeter = new(MeterName);

    public static Counter<long> MessageCount = TestMeter.CreateCounter<long>("app_message_total");

    public static Counter<long> ErrorCount = TestMeter.CreateCounter<long>("app_errors_total");
    
    public static Histogram<double>
        MessageLatency = TestMeter.CreateHistogram<double>("app_message_latency", "seconds");
    
    public static MeterProviderBuilder AddTestMetrics(this MeterProviderBuilder builder)
    {
        builder
            .AddMeter(MeterName)
            .AddView("app_message_latency", new ExplicitBucketHistogramConfiguration
            {
                Boundaries = new[] {0.001, 0.002, 0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1, 2, 5, 10}
            });

        return builder;
    }
}