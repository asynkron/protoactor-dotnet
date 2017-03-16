using System;
using Xunit;

namespace Proto.Tests
{
    public class FutureTests
    {
        [Fact]
        public void Given_Actor_When_AwaitRequestAsync_Should_ReturnReply()
        {
            var pid = Actor.Spawn(Actor.FromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.Respond("hey");
                }
                return Actor.Done;
            }));

            var reply = pid.RequestAsync<object>("hello").Result;

            Assert.Equal("hey", reply);
        }

        [Fact]
        public void Given_Actor_When_AwaitContextRequestAsync_Should_GetReply()
        {
            var pid1 = Actor.Spawn(Actor.FromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.Respond("hey");
                }
                return Actor.Done;
            }));
            var pid2 = Actor.Spawn(Actor.FromFunc(async ctx =>
            {
                if (ctx.Message is string)
                {
                    var reply1 = await ctx.RequestAsync<string>(pid1, "");
                    ctx.Respond(ctx.Message + reply1);
                }
            }));
            

            var reply2 = pid2.RequestAsync<string>("hello").Result;

            Assert.Equal("hellohey", reply2);
        }

        [Fact]
        public void Given_Actor_When_ReplyIsNull_Should_Return()
        {
            var pid = Actor.Spawn(Actor.FromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.Respond(null);
                }
                return Actor.Done;
            }));

            var reply = pid.RequestAsync<object>("hello", TimeSpan.FromSeconds(1)).Result;

            Assert.Equal(null, reply);
        }
    }
}
