using System;
using System.Threading.Tasks;
using Proto.TestKit;
using Xunit;
using static Proto.TestFixtures.Receivers;

namespace Proto.Tests;

internal class MyAutoRespondMessage : IAutoRespond
{
    public object GetAutoResponse(IContext context) => "hey";
}

public class ActorTests
{
    [Fact]
    public async Task CanNotSpawnAfterShutdown()
    {
        await using var system = new ActorSystem();
        var context = system.Root;

        var pid = context.Spawn(Props.FromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.Respond("hey");
                }

                return Task.CompletedTask;
            }
        ));

        var reply = await context.RequestAsync<object>(pid, "hello");
        Assert.Equal("hey", reply);

        await system.ShutdownAsync();
        
        var pid2 = context.Spawn(Props.FromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.Respond("hey");
                }

                return Task.CompletedTask;
            }
        ));
        
        Assert.Same(system.DeadLetterPid, pid2);
    }
    
    [Fact]
    public async Task CanNotSendUserMessageAfterShutdown()
    {
        await using var system = new ActorSystem();
        var context = system.Root;

        var pid = context.Spawn(Props.FromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.Respond("hey");
                }

                return Task.CompletedTask;
            }
        ));

        var reply = await context.RequestAsync<object>(pid, "hello");
        Assert.Equal("hey", reply);

        await system.ShutdownAsync();
        
        //expect DeadLetterException
        
        await Assert.ThrowsAsync<DeadLetterException>(async () =>
        {
            await context.RequestAsync<object>(pid, "hello");
        }); 
    }
    
    [Fact]
    public async Task RequestActorAsync()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;

        var pid = context.Spawn(Props.FromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.Respond("hey");
                }

                return Task.CompletedTask;
            }
        ));

        var reply = await context.RequestAsync<object>(pid, "hello");

        Assert.Equal("hey", reply);
    }

    [Fact]
    public async Task RequestActorAsyncCanTouchActor()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;

        //no code...
        var pid = context.Spawn(Props.FromFunc(ctx => Task.CompletedTask));

        var reply = await context.RequestAsync<Touched>(pid, new Proto.Touch(), CancellationTokens.FromSeconds(5));

        Assert.Equal(pid, reply.Who);
    }

    [Fact]
    public async Task RequestActorAsyncAutoRespond()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;

        //no code...
        var pid = context.Spawn(Props.FromFunc(ctx => Task.CompletedTask));

        var reply = await context.RequestAsync<object>(pid, new MyAutoRespondMessage());

        Assert.Equal("hey", reply);
    }

    [Fact]
    public async Task RequestActorAsync_should_raise_TimeoutException_when_timeout_is_reached()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;

        var pid = context.Spawn(Props.FromFunc(EmptyReceive));

        var timeoutEx = await Assert.ThrowsAsync<TimeoutException>(
            () => { return context.RequestAsync<object>(pid, "", TimeSpan.FromMilliseconds(20)); }
        );

        Assert.Equal("Request didn't receive any Response within the expected time.", timeoutEx.Message);
    }

    [Fact]
    public async Task RequestActorAsync_should_not_raise_TimeoutException_when_result_is_first()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;

        var pid = context.Spawn(Props.FromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.Respond("hey");
                }

                return Task.CompletedTask;
            }
        ));

        var reply = await context.RequestAsync<object>(pid, "hello", TimeSpan.FromMilliseconds(1000));

        Assert.Equal("hey", reply);
    }

    [Fact]
    public async Task ActorLifeCycle()
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var (probe, probePid) = system.CreateTestProbe();

        var pid = context.Spawn(
            Props.FromFunc(ctx =>
            {
                ctx.Send(probePid, ctx.Message);
                return Task.CompletedTask;
            })
        );

        context.Send(pid, "hello");

        await probe.ExpectNextSystemMessageAsync<Started>();
        await probe.ExpectNextUserMessageAsync<string>(s => s == "hello");
        await context.StopAsync(pid);
        await probe.ExpectNextSystemMessageAsync<Stopping>();
        await probe.ExpectNextSystemMessageAsync<Stopped>();
        await probe.ExpectEmptyMailboxAsync();
    }

    [Fact]
    public async Task ActorLifeCycleWhenExceptionIsThrown()
    {
        await using var system = new ActorSystem();
        var context = system.Root;

        var (probe, probePid) = system.CreateTestProbe();
        var i = 0;
        CapturedContext? capturedContext = null;

        var pid = context.Spawn(
            Props.FromFunc(async ctx =>
            {
                ctx.Send(probePid, ctx.Message);

                if (ctx.Message is string && i++ == 0)
                {
                    capturedContext = ctx.Capture();
                    throw new Exception("Test");
                }

                if (ctx.Message is Started && capturedContext != null)
                {
                    await capturedContext.Receive();
                }

                await Task.Yield();
            })
        );

        context.Send(pid, "hello");
        context.Send(pid, "hello");
        await context.PoisonAsync(pid);

        await probe.ExpectNextSystemMessageAsync<Started>();
        await probe.ExpectNextUserMessageAsync<string>(s => s == "hello");
        await probe.ExpectNextUserMessageAsync<Restarting>();
        await probe.ExpectNextSystemMessageAsync<Started>();
        await probe.ExpectNextUserMessageAsync<string>(s => s == "hello");
        await probe.ExpectNextUserMessageAsync<string>(s => s == "hello");
        await probe.ExpectNextSystemMessageAsync<Stopping>();
        await probe.ExpectNextSystemMessageAsync<Stopped>();
        await probe.ExpectEmptyMailboxAsync();
    }

    [Fact]
    public async Task StopActorWithLongRunningTask()
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var (probe, probePid) = system.CreateTestProbe();

        var pid = context.Spawn(
            Props.FromFunc(async ctx =>
            {
                if (ctx.Message is string)
                {
                    try
                    {
                        await Task.Delay(5000, ctx.CancellationToken);
                    }
                    catch (Exception e)
                    {
                        ctx.Send(probePid, e);
                    }
                }

                ctx.Send(probePid, ctx.Message);
            })
        );

        context.Send(pid, "hello");
        await probe.ExpectNextSystemMessageAsync<Started>();
        await context.StopAsync(pid);
        await probe.ExpectNextUserMessageAsync<TaskCanceledException>();
        await probe.ExpectNextUserMessageAsync<string>(s => s == "hello");
        await probe.ExpectNextSystemMessageAsync<Stopping>();
        await probe.ExpectNextSystemMessageAsync<Stopped>();
        await probe.ExpectEmptyMailboxAsync();
    }

    [Fact]
    public async Task ForwardActorAsync()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;

        var pid = context.Spawn(Props.FromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.Respond("hey");
                }

                return Task.CompletedTask;
            }
        ));

        var forwarder = context.Spawn(Props.FromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.Forward(pid);
                }

                return Task.CompletedTask;
            }
        ));

        var reply = await context.RequestAsync<object>(forwarder, "hello");

        Assert.Equal("hey", reply);
    }
}
