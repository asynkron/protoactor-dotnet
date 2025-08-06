using System;
using System.Threading.Tasks;
using Proto.Timers;
using Xunit;
#if NET8_0_OR_GREATER
using Microsoft.Extensions.Time.Testing;
#endif

namespace Proto.Tests;

public class TimerExtensionsTests
{
    [Fact]
    public async Task SchedulerSchedulesMessageAfterDelay()
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var tcs = new TaskCompletionSource();

        var pid = context.Spawn(Props.FromFunc(ctx =>
        {
            if (ctx.Message is "Wakeup")
            {
                tcs.SetResult();
            }

            return Task.CompletedTask;
        }));

        var scheduler = context.Scheduler();

        scheduler.SendOnce(TimeSpan.FromMilliseconds(200), pid, "Wakeup");

        // ensure message isn't delivered immediately
        await Task.Delay(100);
        Assert.False(tcs.Task.IsCompleted);

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

#if NET8_0_OR_GREATER
    [Fact]
    public async Task SchedulerWithTimeProviderSchedulesMessageAfterDelay()
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var tcs = new TaskCompletionSource();

        var pid = context.Spawn(Props.FromFunc(ctx =>
        {
            if (ctx.Message is "Wakeup")
            {
                tcs.SetResult();
            }

            return Task.CompletedTask;
        }));

        var timeProvider = new FakeTimeProvider();
        var scheduler = context.Scheduler(timeProvider);

        scheduler.SendOnce(TimeSpan.FromSeconds(10), pid, "Wakeup");

        // Give the inner Task.Delay a head start
        await Task.Delay(50);
        timeProvider.Advance(TimeSpan.FromMinutes(1));

        await tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(10));
    }
#endif
}
