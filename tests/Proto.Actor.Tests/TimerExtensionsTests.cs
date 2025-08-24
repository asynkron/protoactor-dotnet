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

        await probe.ExpectNextUserMessageAsync<string>(s => s == "Wakeup");
    }

    [Fact]
    public async Task SchedulerWithTimeProviderSchedulesMessageAfterDelay()
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var (probe, pid) = system.CreateTestProbe();

        var timeProvider = new FakeTimeProvider();
        var hook = new TestSchedulerHook();
        var scheduler = context.Scheduler(timeProvider, hook);

        scheduler.SendOnce(TimeSpan.FromSeconds(10), pid, "Wakeup");

        await hook.WaitAsync();
        timeProvider.Advance(TimeSpan.FromMinutes(1));

        await probe.ExpectNextUserMessageAsync<string>(s => s == "Wakeup");
    }

    private sealed class TestSchedulerHook : ISchedulerHook
    {
        private readonly TaskCompletionSource _tcs = new();

        public Task WaitAsync() => _tcs.Task;

        public void OnTimerRegistered() => _tcs.TrySetResult();
    }
}
