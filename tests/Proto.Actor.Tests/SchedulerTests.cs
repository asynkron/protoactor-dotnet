#if NET8_0_OR_GREATER
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Proto.Timers;
using Xunit;

namespace Proto.Tests;

public class SchedulerTests
{
    [Fact]
    public async Task SendOnceWorksWithTimeProvider()
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var tcs = new TaskCompletionSource();
        var pid = context.Spawn(Props.FromFunc(context =>
        {
            if (context.Message is "Wakeup")
            {
                tcs.SetResult();
            }

            return Task.CompletedTask;
        }));
        var timeProvider = new FakeTimeProvider();
        var scheduler = context.Scheduler(timeProvider);

        scheduler.SendOnce(TimeSpan.FromSeconds(10), pid, "Wakeup");
        // Give SendOnce's inner call to Task.Delay a head start, so it won't miss the call to Advance.
        await Task.Delay(50);
        timeProvider.Advance(TimeSpan.FromMinutes(10));
        await tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(10));
    }

    [Fact]
    public async Task SendRepeatedlyCanBeCancelled()
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var count = 0;
        var firstMessage = new TaskCompletionSource();
        var extraMessage = new TaskCompletionSource();

        var pid = context.Spawn(Props.FromFunc(ctx =>
        {
            if (ctx.Message is "Tick")
            {
                count++;

                if (count == 1)
                {
                    firstMessage.SetResult();
                }
                else
                {
                    extraMessage.SetResult();
                }
            }

            return Task.CompletedTask;
        }));

        var timeProvider = new FakeTimeProvider();
        var scheduler = context.Scheduler(timeProvider);

        var cts = scheduler.SendRepeatedly(TimeSpan.FromSeconds(5), pid, "Tick");

        // Give SendRepeatedly's inner call to Task.Delay a head start
        await Task.Delay(50);
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        await firstMessage.Task.WaitAsync(TimeSpan.FromMilliseconds(10));

        cts.Cancel();

        timeProvider.Advance(TimeSpan.FromMinutes(1));
        await Task.Delay(50);

        Assert.Equal(1, count);
        Assert.False(extraMessage.Task.IsCompleted);
    }
}
#endif
