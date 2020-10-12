using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Tests
{
    public class ReenterTests
    {
        private static readonly ActorSystem System = new ActorSystem();
        private static readonly RootContext Context = System.Root;

        private readonly ITestOutputHelper output;

        public ReenterTests(ITestOutputHelper output)
        {
            this.output = output;
        }


        [Fact]
        public async Task ReenterAfterCompletedTask()
        {
            
            var props = Props.FromFunc(ctx =>
                {
                    if ( ctx.Message is string str && str == "reenter")
                    {
                        var delay = Task.Delay(500);
                        ctx.ReenterAfter(delay, () =>
                        {
                            ctx.Respond("response");
                        });
                    }
                    return Task.CompletedTask;
                }
            );

            var pid = Context.Spawn(props);
            
            var res = await Context.RequestAsync<string>(pid, "reenter",TimeSpan.FromSeconds(1));
            Assert.Equal("response",res);
        }
        
        [Fact]
        public async Task ReenterAfterHonorsActorConcurrency()
        {
            var activeCount = 0;
            var correct = true;
            var props = Props.FromFunc(async ctx =>
                {
                    var res = Interlocked.Increment(ref activeCount);
                    if (res != 1)
                    {
                        correct = false;
                    }

                    await Task.Yield();
                    Interlocked.Decrement(ref activeCount);
                }
            );

            var pid = Context.Spawn(props);

            //concurrency yolo, no way to force a failure, especially not if the implementation is correct, as expected
            for (var i = 0; i < 100000; i++)
            {
                Context.Send(pid, "msg");    
            }

            await Context.PoisonAsync(pid);
            Assert.True(correct);

        }
    }
}