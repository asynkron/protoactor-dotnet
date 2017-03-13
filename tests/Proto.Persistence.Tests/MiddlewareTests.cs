using System;
using System.Threading.Tasks;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Persistence.Tests
{
    public class MiddlewareTests
    {
        [Fact]
        public void Should_Call_Next_Middleware_On_Started()
        {
            var provider = new InMemoryProvider();
            var wasCalled = false;
            var actor = Actor.Spawn(
                Actor.FromFunc(context =>
                {
                    if (context.Message is Started)
                        wasCalled = true;
                    return Task.FromResult(0);
                })
                .WithReceiveMiddleware(Persistence.Using(provider))
                .WithMailbox(() => new TestMailbox())
                );

            Assert.True(wasCalled);
        }

        [Fact]
        public void Should_Call_Next_Middleware_On_UserMessage()
        {
            var provider = new InMemoryProvider();
            var wasCalled = false;
            var actor = Actor.Spawn(
                Actor.FromFunc(context =>
                {
                    if (context.Message is string)
                        wasCalled = true;
                    return Task.FromResult(0);
                })
                .WithReceiveMiddleware(Persistence.Using(provider))
                .WithMailbox(() => new TestMailbox())
                );

            actor.Tell("hello");

            Assert.True(wasCalled);
        }
    }
}
