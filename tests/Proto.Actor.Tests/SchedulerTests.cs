using System;
using System.Threading.Tasks;
using System.Threading;
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
        var hook = new TestSchedulerHook();
        var scheduler = context.Scheduler(timeProvider, hook);

        scheduler.SendOnce(TimeSpan.FromSeconds(10), pid, "Wakeup");
        await hook.WaitAsync();
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
        var hook = new TestSchedulerHook();
        var scheduler = context.Scheduler(timeProvider, hook);

        var cts = scheduler.SendRepeatedly(TimeSpan.FromSeconds(5), pid, "Tick");

        await hook.WaitAsync();
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        await firstMessage.Task.WaitAsync(TimeSpan.FromMilliseconds(10));

        cts.Cancel();

        timeProvider.Advance(TimeSpan.FromMinutes(1));
        await Task.Delay(50);

        Assert.Equal(1, count);
        Assert.False(extraMessage.Task.IsCompleted);
    }

    [Fact]
    public async Task SendOnceCanBeCancelled()
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
        var hook = new TestSchedulerHook();
        var scheduler = context.Scheduler(timeProvider, hook);

        var cts = scheduler.SendOnce(TimeSpan.FromSeconds(5), pid, "Wakeup");

        await hook.WaitAsync();
        cts.Cancel();

        timeProvider.Advance(TimeSpan.FromMinutes(1));
        await Task.Delay(50);

        Assert.False(tcs.Task.IsCompleted);
    }

    [Fact]
    public async Task RequestRepeatedlyCanBeCancelled()
    {
        await using var system = new ActorSystem();
        var context = system.Root;

        var responder = context.Spawn(Props.FromFunc(ctx =>
        {
            if (ctx.Message is "Ping")
            {
                ctx.Respond("Pong");
            }

            return Task.CompletedTask;
        }));

        var firstResponse = new TaskCompletionSource();
        var extraResponse = new TaskCompletionSource();

        var timeProvider = new FakeTimeProvider();
        var hook = new TestSchedulerHook();

        CancellationTokenSource? cts = null;
        var requester = context.Spawn(Props.FromFunc(ctx =>
        {
            switch (ctx.Message)
            {
                case Started:
                    var scheduler = ctx.Scheduler(timeProvider, hook);
                    cts = scheduler.RequestRepeatedly(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), responder, "Ping");
                    break;
                case string msg when msg == "Pong":
                    if (!firstResponse.Task.IsCompleted)
                    {
                        firstResponse.SetResult();
                    }
                    else
                    {
                        extraResponse.SetResult();
                    }
                    break;
                case "Cancel":
                    cts?.Cancel();
                    break;
            }

            return Task.CompletedTask;
        }));

        await hook.WaitAsync();
        timeProvider.Advance(TimeSpan.FromSeconds(5));
        await firstResponse.Task.WaitAsync(TimeSpan.FromMilliseconds(10));

        context.Send(requester, "Cancel");

        // ensure cancellation is processed before advancing time
        await Task.Delay(50);

        timeProvider.Advance(TimeSpan.FromSeconds(10));
        await Task.Delay(50);

        Assert.False(extraResponse.Task.IsCompleted);
    }

    private sealed class TestSchedulerHook : ISchedulerHook
    {
        private readonly TaskCompletionSource _tcs = new();

        public Task WaitAsync() => _tcs.Task;

        public void OnTimerRegistered() => _tcs.TrySetResult();
    }
}

