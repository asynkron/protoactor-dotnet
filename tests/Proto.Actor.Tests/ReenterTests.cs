using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Tests
{
    public class ReenterTests : ActorTestBase
    {
        private readonly ITestOutputHelper _output;

        public ReenterTests(ITestOutputHelper output) => _output = output;

        [Fact]
        public async Task RequestReenterSelf()
        {
            var props = Props.FromFunc(async ctx => {
                    switch (ctx.Message)
                    {
                        case "reenter":
                            await Task.Delay(500);
                            ctx.Respond("done");
                            break;
                        case "start":
                            ctx.RequestReenter<string>(ctx.Self, "reenter", t => {
                                    ctx.Respond("response");
                                    return Task.CompletedTask;
                                }, CancellationToken.None
                            );
                            break;
                    }
                }
            );

            var pid = Context.Spawn(props);

            var res = await Context.RequestAsync<string>(pid, "start", TimeSpan.FromSeconds(5));
            Assert.Equal("response", res);
        }

        [Fact]
        public async Task ReenterAfterCompletedTask()
        {
            var props = Props.FromFunc(ctx => {
                    if (ctx.Message is "reenter")
                    {
                        var delay = Task.Delay(500);
                        ctx.ReenterAfter(delay, () => { ctx.Respond("response"); });
                    }

                    return Task.CompletedTask;
                }
            );

            var pid = Context.Spawn(props);

            var res = await Context.RequestAsync<string>(pid, "reenter", TimeSpan.FromSeconds(5));
            Assert.Equal("response", res);
        }

        [Fact]
        public async Task ReenterAfterTimerCancelledToken()
        {
            var props = Props.FromProducer(() => new ReenterAfterCancellationActor());

            var pid = Context.Spawn(props);

            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            var request = new ReenterAfterCancellationActor.Request(cancellationTokenSource.Token);

            var res = await Context.RequestAsync<ReenterAfterCancellationActor.Response>(pid, request, TimeSpan.FromSeconds(5));
            res.Should().NotBeNull();
        }

        [Fact]
        public async Task ReenterAfterAlreadyCancelledToken()
        {
            var props = Props.FromProducer(() => new ReenterAfterCancellationActor());

            var pid = Context.Spawn(props);

            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();
            var request = new ReenterAfterCancellationActor.Request(cancellationTokenSource.Token);

            var res = await Context.RequestAsync<ReenterAfterCancellationActor.Response>(pid, request, TimeSpan.FromSeconds(5));
            res.Should().NotBeNull();
        }

        [Fact]
        public async Task NoReenterAfterNonCancellableToken()
        {
            var props = Props.FromProducer(() => new ReenterAfterCancellationActor());

            var pid = Context.Spawn(props);

            var request = new ReenterAfterCancellationActor.Request(CancellationToken.None);

            await Context.Invoking(async ctx
                    => await Context.RequestAsync<ReenterAfterCancellationActor.Response>(pid, request, TimeSpan.FromMilliseconds(500))
                ).Should()
                .ThrowExactlyAsync<TimeoutException>();
        }

        [Fact]
        public async Task ReenterAfterFailedTask()
        {
            var props = Props.FromFunc(ctx => {
                    if (ctx.Message is "reenter")
                    {
                        var task = Task.Run(async () => {
                                await Task.Delay(100);
                                throw new Exception("Failed!");
                            }
                        );
                        ctx.ReenterAfter(task, () => { ctx.Respond("response"); });
                    }

                    return Task.CompletedTask;
                }
            );

            var pid = Context.Spawn(props);

            var res = await Context.RequestAsync<string>(pid, "reenter", TimeSpan.FromSeconds(5));
            Assert.Equal("response", res);
        }

        [Fact]
        public async Task ReenterAfterCancelledTask()
        {
            var props = Props.FromFunc(ctx => {
                    if (ctx.Message is "reenter")
                    {
                        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        ctx.ReenterAfter(tcs.Task, _ => {
                                ctx.Respond("response");
                                return Task.CompletedTask;
                            }
                        );
                        tcs.TrySetCanceled();
                    }

                    return Task.CompletedTask;
                }
            );

            var pid = Context.Spawn(props);

            var res = await Context.RequestAsync<string>(pid, "reenter", TimeSpan.FromSeconds(5));
            Assert.Equal("response", res);
        }

        [Fact]
        public async Task ReenterAfterHonorsActorConcurrency()
        {
            var activeCount = 0;
            var correct = true;
            var counter = 0;
            var props = Props.FromFunc(ctx => {
                    if (ctx.Message is string msg && msg == "reenter")
                    {
                        //use ++ on purpose, any race condition would make the counter go out of sync
                        counter++;

                        var task = Task.Delay(0);
                        ctx.ReenterAfter(task, () => {
                                var res = Interlocked.Increment(ref activeCount);
                                if (res != 1) correct = false;

                                Interlocked.Decrement(ref activeCount);
                            }
                        );
                    }

                    return Task.CompletedTask;
                }
            );

            var pid = Context.Spawn(props);

            //concurrency yolo, no way to force a failure, especially not if the implementation is correct, as expected
            for (var i = 0; i < 100000; i++)
            {
                Context.Send(pid, "reenter");
            }

            await Context.PoisonAsync(pid);
            Assert.True(correct);
            Assert.Equal(100000, counter);
        }

        [Fact]
        public async Task DropReenterContinuationAfterRestart()
        {
            bool restarted = false;
            bool completionExecuted = false;
            var props = Props.FromFunc(async ctx => {
                switch (ctx.Message)
                {
                    case "start":
                        CancellationTokenSource cts = new();
                        ctx.ReenterAfter(
                            Task.Delay(-1, cts.Token),
                            () => {
                                completionExecuted = true;
                            });
                        ctx.Self.SendSystemMessage(ctx.System, new Restart(new Exception()));
                        // Release the cancellation token after restart gets processed.
                        cts.Cancel();
                        ctx.Respond(true);
                        break;
                    case Restarting:
                        restarted = true;
                        break;
                    case "waitstate":
                        // Wait a while to make sure that Completion really didn't execute.
                        Task.Delay(50);
                        while (!ctx.CancellationToken.IsCancellationRequested)
                        {
                            await Task.Yield();
                            if (restarted && !completionExecuted)
                            {
                                ctx.Respond(true);
                                break;
                            }
                        }
                        break;
                }
            }
            );

            var pid = Context.Spawn(props);

            await Context.RequestAsync<bool>(pid, "start", TimeSpan.FromSeconds(5));
            var res = await Context.RequestAsync<bool>(pid, "waitstate", TimeSpan.FromSeconds(5));
            Assert.True(res);
        }

        private class ReenterAfterCancellationActor : IActor
        {
            public Task ReceiveAsync(IContext context)
            {
                switch (context.Message)
                {
                    case Request request:
                        context.ReenterAfterCancellation(request.Token, () => context.Respond(new Response()));
                        break;
                }

                return Task.CompletedTask;
            }

            public record Request(CancellationToken Token);

            public record Response;
        }
    }
}
