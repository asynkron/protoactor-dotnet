using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Proto.Tests
{
    public class ActorTests
    {
        static PID SpawnActorFromFunc(Receive receive) => Actor.Spawn(Actor.FromFunc(receive));


        [Fact]
        public void RequestActorAsync()
        {
            PID pid = SpawnActorFromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.Respond("hey");
                }
                return Actor.Done;
            });

            var reply = pid.RequestAsync<object>("hello").Result;

            Assert.Equal("hey", reply);
        }


        [Fact]
        public async Task RequestActorAsync_should_raise_TimeoutException_when_timeout_is_reached()
        {
            PID pid = SpawnActorFromFunc(ctx =>
            {
                // Do not reply
                return Actor.Done;
            });


            var timeoutEx = await Assert.ThrowsAsync<TimeoutException>(() => pid.RequestAsync<object>("", TimeSpan.FromMilliseconds(100)));
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
            var are = new AutoResetEvent(false);

            var messages = new Queue<object>();

            var pid = SpawnActorFromFunc(ctx =>
            {
                messages.Enqueue(ctx.Message);
                are.Set();
                return Actor.Done;
            });

            pid.Request("hello", null);
            are.WaitOne(100); // Started
            are.WaitOne(100); // string

            pid.Stop();
            are.WaitOne(100); // Stopping
            are.WaitOne(100); // Stopped

            // to prevent the Stop sequence to grow longer in the future without updating this test
            Thread.Sleep(1000);

            Assert.Equal(4, messages.Count);
            var msgs = messages.ToArray();
            Assert.IsType(typeof(Started), msgs[0]);
            Assert.IsType(typeof(string), msgs[1]);
            Assert.IsType(typeof(Stopping), msgs[2]);
            Assert.IsType(typeof(Stopped), msgs[3]);
        }
    }
}
