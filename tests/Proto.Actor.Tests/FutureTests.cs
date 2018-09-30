using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Tests
{
    public class FutureTests
    {
        private readonly ITestOutputHelper output;

        public FutureTests(ITestOutputHelper output)
        {
            this.output = output;
        }
        
        private static readonly RootContext Context = new RootContext();
        
        [Fact]
        public void Given_Actor_When_AwaitRequestAsync_Should_ReturnReply()
        {
            var pid = Context.Spawn(Props.FromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.Respond("hey");
                }
                return Actor.Done;
            }));

            var reply = Context.RequestAsync<object>(pid, "hello").Result;

            Assert.Equal("hey", reply);
        }

        [Fact]
        public void Given_Actor_When_AwaitContextRequestAsync_Should_GetReply()
        {
            var pid1 = Context.Spawn(Props.FromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.Respond("hey");
                }
                return Actor.Done;
            }));
            var pid2 = Context.Spawn(Props.FromFunc(async ctx =>
            {
                if (ctx.Message is string)
                {
                    var reply1 = await ctx.RequestAsync<string>(pid1, "");
                    ctx.Respond(ctx.Message + reply1);
                }
            }));
            

            var reply2 = Context.RequestAsync<string>(pid2, "hello").Result;

            Assert.Equal("hellohey", reply2);
        }

        [Fact]
        public void Given_Actor_When_ReplyIsNull_Should_Return()
        {
            var pid = Context.Spawn(Props.FromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.Respond(null);
                }
                return Actor.Done;
            }));

            var reply = Context.RequestAsync<object>(pid, "hello", TimeSpan.FromSeconds(1)).Result;

            Assert.Null(reply);
        }
        
        [Fact]
        public void TestInATask() // This one will break
        {
            Task.Run(async () =>
            {
                var pid = Context.Spawn(Props.FromFunc(ctx =>
                {
                    if (ctx.Message is string msg)
                    {
                        output.WriteLine("Got Message " + msg);
                        ctx.Respond(null);
                        output.WriteLine("Sent Response to " + msg);
                    }
                    return Actor.Done;
                }));

                output.WriteLine("Starting");
                var reply1 = await Context.RequestAsync<object>(pid, "hello1", TimeSpan.FromSeconds(2));
                Assert.Null(reply1);
                output.WriteLine("got response 1");
                var reply2 = Context.RequestAsync<object>(pid, "hello2", TimeSpan.FromSeconds(2)).Result;
                Assert.Null(reply2);
                output.WriteLine("got response 2");
            }).Wait();
        }
    }
}
