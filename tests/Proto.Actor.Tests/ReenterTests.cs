using System;
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
    }
}