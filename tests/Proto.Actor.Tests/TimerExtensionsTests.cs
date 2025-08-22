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
        var (probe, pid) = system.CreateTestProbe();

        var scheduler = context.Scheduler();

        scheduler.SendOnce(TimeSpan.FromMilliseconds(200), pid, "Wakeup");

        // ensure message isn't delivered immediately
        await probe.ExpectNoMessageAsync(TimeSpan.FromMilliseconds(100));

        await probe.GetNextMessageAsync<string>(s => s == "Wakeup", TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SchedulerWithTimeProviderSchedulesMessageAfterDelay()
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var (probe, pid) = system.CreateTestProbe();

        var timeProvider = new FakeTimeProvider();
        var scheduler = context.Scheduler(timeProvider);

        scheduler.SendOnce(TimeSpan.FromSeconds(10), pid, "Wakeup");

        // Give the inner Task.Delay a head start
        await Task.Delay(50);
        timeProvider.Advance(TimeSpan.FromMinutes(1));

        await probe.GetNextMessageAsync<string>(s => s == "Wakeup", TimeSpan.FromMilliseconds(100));
    }
}

