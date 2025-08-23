using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Proto.Utils;
using Xunit;

namespace Proto.Tests.Utils;

public class ThrottleTests
{
    [Fact]
    public void ThrottlesEfficiently()
    {
        const int maxEvents = 2;
        var triggered = 0;
        var shouldThrottle = Throttle.Create(maxEvents, TimeSpan.FromSeconds(1), null, CancellationToken.None);

        for (var i = 0; i < 10000; i++)
        {
            if (shouldThrottle().IsOpen())
            {
                triggered++;
            }
        }

        triggered.Should().Be(maxEvents);
    }

    [Fact]
    public async Task OpensAfterTimespan()
    {
        const int maxEvents = 2;
        var triggered = 0;
        var shouldThrottle = Throttle.Create(maxEvents, TimeSpan.FromMilliseconds(50), null, CancellationToken.None);

        for (var i = 0; i < 100; i++)
        {
            if (shouldThrottle().IsOpen())
            {
                triggered++;
            }
        }

        triggered.Should().Be(maxEvents);

        await Task.Delay(2000); // wait for throttle window to reset

        for (var i = 0; i < 100; i++)
        {
            if (shouldThrottle().IsOpen())
            {
                triggered++;
            }
        }

        triggered.Should().Be(maxEvents * 2, "We expect the throttle to open after the timespan");
    }

    [Fact]
    public async Task GivesCorrectValveStatus()
    {
        const int maxEvents = 2;
        var shouldThrottle = Throttle.Create(maxEvents, TimeSpan.FromMilliseconds(50), null, CancellationToken.None);

        shouldThrottle().Should().Be(Throttle.Valve.Open, "It accepts multiple event before closing");
        shouldThrottle().Should().Be(Throttle.Valve.Closing, "Last event before close");
        shouldThrottle().Should().Be(Throttle.Valve.Closed, "Anything over the limit is throttled");
        await Task.Delay(1000); // allow throttle to reopen
        shouldThrottle().Should().Be(Throttle.Valve.Open, "After the period it should open again");
    }
}