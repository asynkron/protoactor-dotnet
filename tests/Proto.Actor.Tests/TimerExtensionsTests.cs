using System;
using System.Threading.Tasks;
using Proto.TestKit;
using Proto.Timers;
using Xunit;
using Microsoft.Extensions.Time.Testing;

namespace Proto.Tests;

public class TimerExtensionsTests
{
    [Fact]
    public async Task SchedulerSchedulesMessageAfterDelay()
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var probe = new TestProbe();
        var pid = context.Spawn(Props.FromProducer(() => probe));

        var scheduler = context.Scheduler();

        scheduler.SendOnce(TimeSpan.FromMilliseconds(200), pid, "Wakeup");

        // ensure message isn't delivered immediately
        await probe.ExpectNoMessageAsync(TimeSpan.FromMilliseconds(100));

        var msg = await probe.GetNextMessageAsync<string>(TimeSpan.FromSeconds(5));
        Assert.Equal("Wakeup", msg);
    }

    [Fact]
    public async Task SchedulerWithTimeProviderSchedulesMessageAfterDelay()
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var probe = new TestProbe();
        var pid = context.Spawn(Props.FromProducer(() => probe));

        var timeProvider = new FakeTimeProvider();
        var scheduler = context.Scheduler(timeProvider);

        scheduler.SendOnce(TimeSpan.FromSeconds(10), pid, "Wakeup");

        // Give the inner Task.Delay a head start
        await Task.Delay(50);
        timeProvider.Advance(TimeSpan.FromMinutes(1));

        var msg = await probe.GetNextMessageAsync<string>(TimeSpan.FromMilliseconds(10));
        Assert.Equal("Wakeup", msg);
    }
}

