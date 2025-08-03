using System;
using System.Threading;
using System.Threading.Tasks;
using Proto.TestFixtures;
using Proto.Utils;
using Xunit;

namespace Proto.Tests;

public class WatchTests
{
    [Fact]
    public async Task MultipleStopsTriggerSingleTerminated()
    {
        await using var system = new ActorSystem();
        var context = system.Root;

        long counter = 0;

        var childProps = Props.FromFunc(ctx =>
            {
                switch (ctx.Message)
                {
                    case Started _:
                        ctx.Stop(ctx.Self);
                        ctx.Stop(ctx.Self);

                        break;
                }

                return Task.CompletedTask;
            }
        );

        context.Spawn(Props.FromFunc(ctx =>
                {
                    switch (ctx.Message)
                    {
                        case Started _:
                            ctx.Spawn(childProps);

                            break;
                        case Terminated _:
                            Interlocked.Increment(ref counter);

                            break;
                    }

                    return Task.CompletedTask;
                }
            )
        );

        await Task.Delay(1000);
        Assert.Equal(1, Interlocked.Read(ref counter));
    }

    [Fact]
    public async Task CanWatchLocalActors()
    {
        await using var system = new ActorSystem();
        var context = system.Root;

        var watchee = context.Spawn(Props.FromProducer(() => new DoNothingActor())
            .WithMailbox(() => new TestMailbox())
        );

        var watcher = context.Spawn(Props.FromProducer(() => new LocalActor(watchee))
            .WithMailbox(() => new TestMailbox())
        );

        await context.StopAsync(watchee);
        var terminatedMessageReceived = await context.RequestAsync<bool>(watcher, "?", TimeSpan.FromSeconds(5));
        Assert.True(terminatedMessageReceived);
    }

    [Fact]
    public async Task UnwatchPreventsTerminatedMessage()
    {
        await using var system = new ActorSystem();
        var context = system.Root;

        var watchee = context.Spawn(Props.FromProducer(() => new DoNothingActor())
            .WithMailbox(() => new TestMailbox())
        );

        var terminated = new TaskCompletionSource<bool>();

        var watcher = context.Spawn(Props.FromFunc(ctx =>
            {
                switch (ctx.Message)
                {
                    case Started _:
                        ctx.Watch(watchee);
                        ctx.Unwatch(watchee);

                        break;
                    case "ready":
                        ctx.Respond(true);

                        break;
                    case Terminated _:
                        terminated.TrySetResult(true);

                        break;
                }

                return Task.CompletedTask;
            }
        ).WithMailbox(() => new TestMailbox())
        );

        await context.RequestAsync<bool>(watcher, "ready", TimeSpan.FromSeconds(5));

        await context.StopAsync(watchee);

        var (completed, _) = await terminated.Task.WaitUpTo(TimeSpan.FromMilliseconds(500));
        Assert.False(completed);
    }

    public class LocalActor : IActor
    {
        private readonly PID _watchee;
        private bool _terminateReceived;

        public LocalActor(PID watchee)
        {
            _watchee = watchee;
        }

        public Task ReceiveAsync(IContext ctx)
        {
            switch (ctx.Message)
            {
                case Started _:
                    ctx.Watch(_watchee);

                    break;
                case string msg when msg == "?":
                    ctx.Respond(_terminateReceived);

                    break;
                case Terminated _:
                    _terminateReceived = true;

                    break;
            }

            return Task.CompletedTask;
        }
    }
}