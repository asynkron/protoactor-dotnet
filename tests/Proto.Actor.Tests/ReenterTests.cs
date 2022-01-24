using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Proto.Context;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Tests
{
    public class ReenterTests
    {
        private static readonly ActorSystem System = new();
        private static readonly RootContext Context = System.Root;

        private readonly ITestOutputHelper output;

        public ReenterTests(ITestOutputHelper output) => this.output = output;

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
        public void NoReenterAfterNonCancellableToken()
        {
            var props = Props.FromProducer(() => new ReenterAfterCancellationActor());

            var pid = Context.Spawn(props);

            var request = new ReenterAfterCancellationActor.Request(CancellationToken.None);

            Context.Invoking(async ctx => await Context.RequestAsync<ReenterAfterCancellationActor.Response>(pid, request, TimeSpan.FromMilliseconds((500)))).Should()
                .ThrowExactly<TimeoutException>();
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

        private class ReenterAfterCancellationActor : IActor
        {

            public record Request(CancellationToken Token);
            public record Response;

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
        } 
    }
}