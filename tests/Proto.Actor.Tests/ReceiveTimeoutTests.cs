using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Proto.Tests;

public class ReceiveTimeoutTests
{
    [Fact]
    public async Task receive_timeout_received_within_expected_time()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;

        var timeoutReceived = false;
        var receiveTimeoutWaiter = GetExpiringTaskCompletionSource();

        var props = Props.FromFunc(ctx =>
            {
                switch (ctx.Message)
                {
                    case Started _:
                        ctx.SetReceiveTimeout(TimeSpan.FromMilliseconds(150));

                        break;
                    case ReceiveTimeout _:
                        timeoutReceived = true;
                        receiveTimeoutWaiter.SetResult(0);

                        break;
                }

                return Task.CompletedTask;
            }
        );

        context.Spawn(props);

        await GetSafeAwaitableTask(receiveTimeoutWaiter);
        Assert.True(timeoutReceived);
    }

    [Fact]
    public async Task receive_timeout_received_within_expected_time_when_sending_ignored_messages()
    {
        
        await using var system = new ActorSystem();
        var context = system.Root;

        var timeoutReceived = false;
        var receiveTimeoutWaiter = GetExpiringTaskCompletionSource(1000);

        var props = Props.FromFunc(ctx =>
            {
                switch (ctx.Message)
                {
                    case Started _:
                        ctx.SetReceiveTimeout(TimeSpan.FromMilliseconds(150));

                        break;
                    case ReceiveTimeout _:
                        timeoutReceived = true;
                        receiveTimeoutWaiter.SetResult(0);

                        break;
                }

                return Task.CompletedTask;
            }
        );

        var pid = context.Spawn(props);

        _ = Task.Run(async () =>
            {
                while (!receiveTimeoutWaiter.Task.IsCompleted)
                {
                    context.Send(pid, new IgnoreMe());
                    await Task.Delay(100);
                }
            }
        );

        await GetSafeAwaitableTask(receiveTimeoutWaiter);
        Assert.True(timeoutReceived);
    }

    [Fact]
    public async Task receive_timeout_not_received_within_expected_time()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;

        var timeoutReceived = false;
        var actorStartedWaiter = GetExpiringTaskCompletionSource();

        var props = Props.FromFunc(ctx =>
            {
                switch (ctx.Message)
                {
                    case Started:
                        ctx.SetReceiveTimeout(TimeSpan.FromMilliseconds(1500));
                        actorStartedWaiter.SetResult(0);

                        break;
                    case ReceiveTimeout:
                        timeoutReceived = true;

                        break;
                }

                return Task.CompletedTask;
            }
        );

        context.Spawn(props);

        await GetSafeAwaitableTask(actorStartedWaiter);
        Assert.False(timeoutReceived);
    }

    [Fact]
    public async Task can_cancel_receive_timeout()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;

        var timeoutReceived = false;
        var endingTimeout = TimeSpan.MaxValue;
        var autoExpiringWaiter = GetExpiringTaskCompletionSource(1500);

        var props = Props.FromFunc(ctx =>
            {
                switch (ctx.Message)
                {
                    case Started _:
                        ctx.SetReceiveTimeout(TimeSpan.FromMilliseconds(150));
                        ctx.CancelReceiveTimeout();
                        endingTimeout = ctx.ReceiveTimeout;

                        break;
                    case ReceiveTimeout _:
                        timeoutReceived = true;
                        autoExpiringWaiter.SetResult(0); // should never happen

                        break;
                }

                return Task.CompletedTask;
            }
        );

        context.Spawn(props);

        // this task should auto cancel
        await GetSafeAwaitableTask(autoExpiringWaiter);

        Assert.True(autoExpiringWaiter.Task.IsCanceled);
        Assert.Equal(TimeSpan.Zero, endingTimeout);
        Assert.False(timeoutReceived);
    }

    [Fact]
    public async Task can_still_set_receive_timeout_after_cancelling()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;

        var timeoutReceived = false;
        var receiveTimeoutWaiter = GetExpiringTaskCompletionSource();

        var props = Props.FromFunc(ctx =>
            {
                switch (ctx.Message)
                {
                    case Started _:
                        ctx.SetReceiveTimeout(TimeSpan.FromMilliseconds(150));
                        ctx.CancelReceiveTimeout();
                        ctx.SetReceiveTimeout(TimeSpan.FromMilliseconds(150));

                        break;
                    case ReceiveTimeout _:
                        timeoutReceived = true;
                        receiveTimeoutWaiter.SetResult(0);

                        break;
                }

                return Task.CompletedTask;
            }
        );

        context.Spawn(props);

        await GetSafeAwaitableTask(receiveTimeoutWaiter);
        Assert.True(timeoutReceived);
    }

    private TaskCompletionSource<int> GetExpiringTaskCompletionSource(int timeoutMs = 60000)
    {
        var tcs = new TaskCompletionSource<int>();
        var ct = new CancellationTokenSource();
        ct.Token.Register(() => tcs.TrySetCanceled());
        ct.CancelAfter(timeoutMs);

        return tcs;
    }

    private ConfiguredTaskAwaitable<Task<int>> GetSafeAwaitableTask(TaskCompletionSource<int> tcs) =>
        tcs.Task
            .ContinueWith(t => t) // suppress any TaskCanceledException
            .ConfigureAwait(false);

    private record IgnoreMe : INotInfluenceReceiveTimeout;
}