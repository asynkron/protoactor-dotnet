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
    public async Task SendRepeatedlyCanBeCancelledWithTimeProvider()
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var counter = 0;
        var first = new TaskCompletionSource();
        var second = new TaskCompletionSource();
        var pid = context.Spawn(Props.FromFunc(ctx =>
        {
            if (ctx.Message is "tick")
            {
                counter++;
                switch (counter)
                {
                    case 1:
                        first.SetResult();
                        break;
                    case 2:
                        second.SetResult();
                        break;
                }
            }

            return Task.CompletedTask;
        }));

        var scheduler = context.Scheduler();

        var cts = scheduler.SendRepeatedly(TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(100), pid, "tick");

        await first.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await second.Task.WaitAsync(TimeSpan.FromSeconds(1));

        cts.Cancel();
        var afterCancel = counter;
        await Task.Delay(200);

        Assert.Equal(afterCancel, counter);
        Assert.Equal(2, counter);
    }
}
#endif
