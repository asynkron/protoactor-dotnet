using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Proto.TestFixtures;
using Xunit;
using static Proto.TestFixtures.Receivers;

namespace Proto.Tests
{
    class MyAutoRespondMessage : IAutoRespond
    {
        public object GetAutoResponse(IContext context) => "hey";
    }
    public class ActorTests
    {
        private static readonly ActorSystem System = new();
        private static readonly RootContext Context = System.Root;

        public static PID SpawnActorFromFunc(Receive receive) => Context.Spawn(Props.FromFunc(receive));

        [Fact]
        public async Task RequestActorAsync()
        {
            var pid = SpawnActorFromFunc(ctx => {
                    if (ctx.Message is string) ctx.Respond("hey");
                    return Task.CompletedTask;
                }
            );

            var reply = await Context.RequestAsync<object>(pid, "hello");

            Assert.Equal("hey", reply);
        }
        
        [Fact]
        public async Task RequestActorAsyncCanTouchActor()
        {
            //no code...
            var pid = SpawnActorFromFunc(ctx => Task.CompletedTask);

            var reply = await Context.RequestAsync<Touched>(pid, new Proto.Touch(), CancellationTokens.FromSeconds(5));

            Assert.Equal(pid, reply.Who);
        }
        
        [Fact]
        public async Task RequestActorAsyncAutoRespond()
        {
            //no code...
            var pid = SpawnActorFromFunc(ctx => Task.CompletedTask);

            var reply = await Context.RequestAsync<object>(pid, new MyAutoRespondMessage());

            Assert.Equal("hey", reply);
        }

        [Fact]
        public async Task RequestActorAsync_should_raise_TimeoutException_when_timeout_is_reached()
        {
            var pid = SpawnActorFromFunc(EmptyReceive);

            var timeoutEx = await Assert.ThrowsAsync<TimeoutException>(
                () => { return Context.RequestAsync<object>(pid, "", TimeSpan.FromMilliseconds(20)); }
            );
            Assert.Equal("Request didn't receive any Response within the expected time.", timeoutEx.Message);
        }

        [Fact]
        public async Task RequestActorAsync_should_not_raise_TimeoutException_when_result_is_first()
        {
            var pid = SpawnActorFromFunc(ctx => {
                    if (ctx.Message is string) ctx.Respond("hey");
                    return Task.CompletedTask;
                }
            );

            var reply = await Context.RequestAsync<object>(pid, "hello", TimeSpan.FromMilliseconds(1000));

            Assert.Equal("hey", reply);
        }

        [Fact]
        public async Task ActorLifeCycle()
        {
            var messages = new Queue<object>();

            var pid = Context.Spawn(
                Props.FromFunc(ctx => {
                            messages.Enqueue(ctx.Message!);
                            return Task.CompletedTask;
                        }
                    )
                    .WithMailbox(() => new TestMailbox())
            );

            Context.Send(pid, "hello");

            await Context.StopAsync(pid);

            Assert.Equal(4, messages.Count);
            var msgs = messages.ToArray();
            Assert.IsType<Started>(msgs[0]);
            Assert.IsType<string>(msgs[1]);
            Assert.IsType<Stopping>(msgs[2]);
            Assert.IsType<Stopped>(msgs[3]);
        }

        [Fact]
        public async Task ActorLifeCycleWhenExceptionIsThrown()
        {
            var messages = new Queue<object>();
            var i = 0;
            async Task HandleMessage(IContext ctx)
            {
                if (ctx.Message is string && i++ == 0)
                {
                    ctx.Stash();
                    throw new Exception("Test");
                }
                messages.Enqueue(ctx.Message!);
                await Task.Yield();
            };
            var pid = Context.Spawn(
                Props.FromFunc(ctx => ctx.Message switch {
                    object => HandleMessage(ctx),
                    _ => Task.CompletedTask
                }
            )
            );

            Context.Send(pid, "hello");
            Context.Send(pid, "hello");

            await Context.PoisonAsync(pid);

            Assert.Equal(7, messages.Count);
            var msgs = messages.ToArray();
            Assert.IsType<Started>(msgs[0]);
            Assert.IsType<Restarting>(msgs[1]);
            Assert.IsType<Started>(msgs[2]);
            Assert.IsType<string>(msgs[3]);
            Assert.IsType<string>(msgs[4]);
            Assert.IsType<Stopping>(msgs[5]);
            Assert.IsType<Stopped>(msgs[6]);
        }

        // [Fact(Skip = "fails on CI")]
        // public async Task StopActorWithLongRunningTask()
        // {
        //     var messages = new Queue<object>();
        //
        //     var pid = Context.Spawn(
        //         Props.FromFunc(async ctx =>
        //             {
        //                 if (ctx.Message is string)
        //                 {
        //                     try
        //                     {
        //                         await Task.Delay(5000, ctx.CancellationToken);
        //                     }
        //                     catch (Exception e)
        //                     {
        //                         messages.Enqueue(e);
        //                     }
        //                 }
        //
        //                 messages.Enqueue(ctx.Message);
        //             })
        //         );
        //
        //     Context.Send(pid, "hello");
        //     // Wait a little while the actor starts to process the message//
        //     await Task.Delay(15);
        //     await Context.StopAsync(pid);
        //
        //     Assert.Equal(5, messages.Count);
        //     var msgs = messages.ToArray();
        //     Assert.IsType<Started>(msgs[0]);
        //     Assert.IsType<TaskCanceledException>(msgs[1]);
        //     Assert.IsType<string>(msgs[2]);
        //     Assert.IsType<Stopping>(msgs[3]);
        //     Assert.IsType<Stopped>(msgs[4]);
        // }

        public static PID SpawnForwarderFromFunc(Receive forwarder) => Context.Spawn(Props.FromFunc(forwarder));

        [Fact]
        public async Task ForwardActorAsync()
        {
            var pid = SpawnActorFromFunc(ctx => {
                    if (ctx.Message is string) ctx.Respond("hey");
                    return Task.CompletedTask;
                }
            );

            var forwarder = SpawnForwarderFromFunc(ctx => {
                    if (ctx.Message is string) ctx.Forward(pid);
                    return Task.CompletedTask;
                }
            );

            var reply = await Context.RequestAsync<object>(forwarder, "hello");

            Assert.Equal("hey", reply);
        }
    }
}