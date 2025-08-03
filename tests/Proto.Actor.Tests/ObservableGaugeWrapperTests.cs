using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using Proto.Metrics;
using Xunit;

namespace Proto.Tests;

public class ObservableGaugeWrapperTests
{
    [Fact]
    public void ObserverManagementWorks()
    {
        var gauge = new ObservableGaugeWrapper<int>();
        Func<IEnumerable<Measurement<int>>> obs1 = () => new[] { new Measurement<int>(1) };
        Func<IEnumerable<Measurement<int>>> obs2 = () => new[] { new Measurement<int>(2) };

        gauge.AddObserver(obs1);
        gauge.AddObserver(obs2);

        var values = gauge.Observe().Select(m => m.Value).ToArray();
        Assert.Contains(1, values);
        Assert.Contains(2, values);

        gauge.RemoveObserver(obs1);
        values = gauge.Observe().Select(m => m.Value).ToArray();
        Assert.DoesNotContain(1, values);
        Assert.Contains(2, values);
    }
}
