using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Proto.TestFixtures;
using Xunit;
using static Proto.TestFixtures.Receivers;

namespace Proto.Tests
{
    public class ActorTests
    {
        public static PID SpawnActorFromFunc(Receive receive) => Actor.Spawn(Actor.FromFunc(receive));


        [Fact]
        public async Task RequestActorAsync()
        {
            PID pid = SpawnActorFromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.Respond("hey");
                }
                return Actor.Done;
            });

            var reply = await pid.RequestAsync<object>("hello");

            Assert.Equal("hey", reply);
        }

        [Fact]
        public async Task RequestActorAsync_should_raise_TimeoutException_when_timeout_is_reached()
        {
            PID pid = SpawnActorFromFunc(EmptyReceive);

            var timeoutEx = await Assert.ThrowsAsync<TimeoutException>(() => pid.RequestAsync<object>("", TimeSpan.FromMilliseconds(20)));
            Assert.Equal("Request didn't receive any Response within the expected time.", timeoutEx.Message);
        }

        [Fact]
        public async Task RequestActorAsync_should_not_raise_TimeoutException_when_result_is_first()
        {
            PID pid = SpawnActorFromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.Respond("hey");
                }
                return Actor.Done;
            });

            var reply = await pid.RequestAsync<object>("hello", TimeSpan.FromMilliseconds(100));

            Assert.Equal("hey", reply);
        }

        [Fact]
        public void ActorLifeCycle()
        {
            var messages = new Queue<object>();

            var pid = Actor.Spawn(
                Actor
                    .FromFunc(ctx =>
                    {
                        messages.Enqueue(ctx.Message);
                        return Actor.Done;
                    })
                    .WithMailbox(() => new TestMailbox())
                );

            pid.Tell("hello");
            pid.Stop();

            Assert.Equal(4, messages.Count);
            var msgs = messages.ToArray();
            Assert.IsType(typeof(Started), msgs[0]);
            Assert.IsType(typeof(string), msgs[1]);
            Assert.IsType(typeof(Stopping), msgs[2]);
            Assert.IsType(typeof(Stopped), msgs[3]);
        }
    }
}
