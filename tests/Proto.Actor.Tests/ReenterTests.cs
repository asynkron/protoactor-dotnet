using System;
using System.Threading;
using System.Threading.Tasks;
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
    }
}