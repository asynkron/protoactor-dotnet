using System;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Time.Testing;
using Proto.TestKit;
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
        var (probe, pid) = system.CreateTestProbe();

        var timeProvider = new FakeTimeProvider();
        var hook = new TestSchedulerHook();
        var scheduler = context.Scheduler(timeProvider, hook);

        scheduler.SendOnce(TimeSpan.FromSeconds(10), pid, "Wakeup");
        await hook.WaitAsync();
        timeProvider.Advance(TimeSpan.FromMinutes(10));
        await probe.ExpectNextUserMessageAsync<string>(x => x == "Wakeup");
    }

    [Fact]
    public async Task SendRepeatedlyCanBeCancelled()
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var (probe, pid) = system.CreateTestProbe();

        var timeProvider = new FakeTimeProvider();
        var hook = new TestSchedulerHook();
        var scheduler = context.Scheduler(timeProvider, hook);

        var cts = scheduler.SendRepeatedly(TimeSpan.FromSeconds(5), pid, "Tick");

        await hook.WaitAsync();
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        await probe.ExpectNextUserMessageAsync<string>();

        cts.Cancel();
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        await probe.ExpectNoMessageAsync(TimeSpan.FromMilliseconds(50));
    }

    [Fact]
    public async Task SendOnceCanBeCancelled()
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var (probe, pid) = system.CreateTestProbe();

        var timeProvider = new FakeTimeProvider();
        var hook = new TestSchedulerHook();
        var scheduler = context.Scheduler(timeProvider, hook);

        var cts = scheduler.SendOnce(TimeSpan.FromSeconds(5), pid, "Wakeup");

        await hook.WaitAsync();
        cts.Cancel();
        // Give the cancellation time to propagate before advancing the clock
        await Task.Delay(50);

        timeProvider.Advance(TimeSpan.FromMinutes(1));
        await probe.ExpectNoMessageAsync(TimeSpan.FromMilliseconds(50));
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

        var (probe, probePid) = system.CreateTestProbe();

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
                    ctx.Send(probePid, msg);
                    break;
                case "Cancel":
                    cts?.Cancel();
                    break;
            }

            return Task.CompletedTask;
        }));

        await hook.WaitAsync();
        timeProvider.Advance(TimeSpan.FromSeconds(5));
        await probe.ExpectNextUserMessageAsync<string>();

        context.Send(requester, "Cancel");

        // ensure cancellation is processed before advancing time
        await Task.Delay(50);

        timeProvider.Advance(TimeSpan.FromSeconds(10));
        // Allow scheduled callbacks to execute after advancing time
        await Task.Delay(50);

        await probe.ExpectNoMessageAsync(TimeSpan.FromMilliseconds(50));
    }

    private sealed class TestSchedulerHook : ISchedulerHook
    {
        private readonly TaskCompletionSource _tcs = new();

        public Task WaitAsync() => _tcs.Task;

        public void OnTimerRegistered() => _tcs.TrySetResult();
    }
}
