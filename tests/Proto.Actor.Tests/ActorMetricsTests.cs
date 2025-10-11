using System.Collections.Generic;
using System.Diagnostics.Metrics;
using Proto.Metrics;
using Xunit;

namespace Proto.Tests;

public class ActorMetricsTests
{
    [Fact]
    public void CounterProducesMeasurement()
    {
        var measurements = new List<long>();
        using var listener = new MeterListener();
        const string spawnCounterName = "protoactor_actor_spawn_count";
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == spawnCounterName)
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) => measurements.Add(value));
        listener.Start();

        ActorMetrics.ActorSpawnCount.Add(5);

        Assert.Contains(5, measurements);
    }
}
