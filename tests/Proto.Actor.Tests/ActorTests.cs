using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
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
        public void RequestActorAsync_throw_exception()
        {
            PID pid = SpawnActorFromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    throw new Exception("haaaa");
                }
                return Actor.Done;
            });

            throw new InvalidOperationException();

            // HOW TO GET THE EXCEPTION ? OR AT LEAST A TIMEOUT ?
            // seems like the parent of the target can receive it, but not the sender.
            // could we break this rule somehow ?

            var reply = pid.RequestAsync<object>("hello").Result;
            Thread.Sleep(1000);
            //Assert.Equal("hey", reply);
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

            // to prevent the Stop sequence to get longer in the future without updating this test
            Thread.Sleep(1000); 

            Assert.Equal(4, messages.Count);
            var msgs = messages.ToArray();
            Assert.IsType(typeof(Started),msgs[0]);
            Assert.IsType(typeof(string), msgs[1]);
            Assert.IsType(typeof(Stopping), msgs[2]);
            Assert.IsType(typeof(Stopped), msgs[3]);
        }
    }
}
