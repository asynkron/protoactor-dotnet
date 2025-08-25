using System;
using System.Threading;
using System.Threading.Tasks;
using Proto.TestKit;
using Proto.Timers;
using Xunit;

namespace Proto.Tests;

public class ReceiveTimeoutTests
{
    [Fact]
    public async Task receive_timeout_received_within_expected_time()
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var (probe, _) = system.CreateTestProbe();

        var props = Props.FromFunc(ctx =>
            {
                switch (ctx.Message)
                {
                    case Started:
                        ctx.SetReceiveTimeout(TimeSpan.FromMilliseconds(150));
                        break;
                    case ReceiveTimeout msg:
                        ctx.Send(probe.Self, msg);
                        break;
                }

                return Task.CompletedTask;
            }
        );

        context.Spawn(props);

        await probe.ExpectNextSystemMessageAsync<ReceiveTimeout>();
    }

    [Fact]
    public async Task receive_timeout_received_within_expected_time_when_sending_ignored_messages()
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var (probe, _) = system.CreateTestProbe();

        var props = Props.FromFunc(ctx =>
            {
                switch (ctx.Message)
                {
                    case Started:
                        ctx.SetReceiveTimeout(TimeSpan.FromMilliseconds(150));
                        break;
                    case ReceiveTimeout msg:
                        ctx.Send(probe.Self, msg);
                        break;
                }

                return Task.CompletedTask;
            }
        );

        var pid = context.Spawn(props);
        var scheduler = context.Scheduler();
        var cts = scheduler.SendRepeatedly(TimeSpan.Zero, TimeSpan.FromMilliseconds(100), pid, new IgnoreMe());

        await probe.ExpectNextSystemMessageAsync<ReceiveTimeout>();
        cts.Cancel();
    }

    [Fact]
    public async Task receive_timeout_is_reset_by_influencing_messages()
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var (probe, _) = system.CreateTestProbe();

        var props = Props.FromFunc(ctx =>
            {
                switch (ctx.Message)
                {
                    case Started:
                        ctx.SetReceiveTimeout(TimeSpan.FromMilliseconds(200));
                        break;
                    case string:
                        // regular messages reset the receive timeout
                        break;
                    case ReceiveTimeout msg:
                        ctx.Send(probe.Self, msg);
                        break;
                }

                return Task.CompletedTask;
            }
        );

        var pid = context.Spawn(props);
        var scheduler = context.Scheduler();
        var cts = scheduler.SendRepeatedly(TimeSpan.Zero, TimeSpan.FromMilliseconds(50), pid, "tick");

        await probe.ExpectNoMessageAsync(TimeSpan.FromMilliseconds(400));
        cts.Cancel();

        await probe.ExpectNextSystemMessageAsync<ReceiveTimeout>(TimeSpan.FromSeconds(2)); // allow time for any in-flight tick messages to complete before timeout fires
    }

    [Fact]
    public async Task receive_timeout_not_received_within_expected_time()
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var (probe, _) = system.CreateTestProbe();

        var props = Props.FromFunc(ctx =>
            {
                switch (ctx.Message)
                {
                    case Started:
                        ctx.SetReceiveTimeout(TimeSpan.FromMilliseconds(1500));
                        ctx.Send(probe.Self, "started");
                        break;
                    case ReceiveTimeout msg:
                        ctx.Send(probe.Self, msg);
                        break;
                }

                return Task.CompletedTask;
            }
        );

        context.Spawn(props);

        await probe.ExpectNextUserMessageAsync<string>();
        await probe.ExpectNoMessageAsync(TimeSpan.FromMilliseconds(200));
    }

    [Fact]
    public async Task can_cancel_receive_timeout()
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var (probe, _) = system.CreateTestProbe();

        var endingTimeout = TimeSpan.MaxValue;

        var props = Props.FromFunc(ctx =>
            {
                switch (ctx.Message)
                {
                    case Started:
                        ctx.SetReceiveTimeout(TimeSpan.FromMilliseconds(150));
                        ctx.CancelReceiveTimeout();
                        endingTimeout = ctx.ReceiveTimeout;
                        break;
                    case ReceiveTimeout msg:
                        ctx.Send(probe.Self, msg); // should never happen
                        break;
                }

                return Task.CompletedTask;
            }
        );

        context.Spawn(props);

        await probe.ExpectNoMessageAsync(TimeSpan.FromMilliseconds(200));

        Assert.Equal(TimeSpan.Zero, endingTimeout);
    }

    [Fact]
    public async Task can_still_set_receive_timeout_after_cancelling()
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var (probe, _) = system.CreateTestProbe();

        var props = Props.FromFunc(ctx =>
            {
                switch (ctx.Message)
                {
                    case Started:
                        ctx.SetReceiveTimeout(TimeSpan.FromMilliseconds(150));
                        ctx.CancelReceiveTimeout();
                        ctx.SetReceiveTimeout(TimeSpan.FromMilliseconds(150));
                        break;
                    case ReceiveTimeout msg:
                        ctx.Send(probe.Self, msg);
                        break;
                }

                return Task.CompletedTask;
            }
        );

        context.Spawn(props);

        await probe.ExpectNextSystemMessageAsync<ReceiveTimeout>();
    }

    private record IgnoreMe : INotInfluenceReceiveTimeout;
}

