using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Proto.TestFixtures;
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
    public async Task RequestActorAsync()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;

        PID SpawnActorFromFunc(Receive receive) => context.Spawn(Props.FromFunc(receive));

        var pid = SpawnActorFromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.Respond("hey");
                }

                return Task.CompletedTask;
            }
        );

        var reply = await context.RequestAsync<object>(pid, "hello");

        Assert.Equal("hey", reply);
    }

    [Fact]
    public async Task RequestActorAsyncCanTouchActor()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;

        PID SpawnActorFromFunc(Receive receive) => context.Spawn(Props.FromFunc(receive));

        //no code...
        var pid = SpawnActorFromFunc(ctx => Task.CompletedTask);

        var reply = await context.RequestAsync<Touched>(pid, new Proto.Touch(), CancellationTokens.FromSeconds(5));

        Assert.Equal(pid, reply.Who);
    }

    [Fact]
    public async Task RequestActorAsyncAutoRespond()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;

        PID SpawnActorFromFunc(Receive receive) => context.Spawn(Props.FromFunc(receive));

        //no code...
        var pid = SpawnActorFromFunc(ctx => Task.CompletedTask);

        var reply = await context.RequestAsync<object>(pid, new MyAutoRespondMessage());

        Assert.Equal("hey", reply);
    }

    [Fact]
    public async Task RequestActorAsync_should_raise_TimeoutException_when_timeout_is_reached()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;

        PID SpawnActorFromFunc(Receive receive) => context.Spawn(Props.FromFunc(receive));

        var pid = SpawnActorFromFunc(EmptyReceive);

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

        PID SpawnActorFromFunc(Receive receive) => context.Spawn(Props.FromFunc(receive));

        var pid = SpawnActorFromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.Respond("hey");
                }

                return Task.CompletedTask;
            }
        );

        var reply = await context.RequestAsync<object>(pid, "hello", TimeSpan.FromMilliseconds(1000));

        Assert.Equal("hey", reply);
    }

    [Fact]
    public async Task ActorLifeCycle()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;

        var messages = new Queue<object>();

        var pid = context.Spawn(
            Props.FromFunc(ctx =>
                    {
                        messages.Enqueue(ctx.Message!);

                        return Task.CompletedTask;
                    }
                )
                .WithMailbox(() => new TestMailbox())
        );

        context.Send(pid, "hello");

        await context.StopAsync(pid);

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
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;

        var messages = new Queue<object>();
        var i = 0;

        CapturedContext? capturedContext = null;

        async Task HandleMessage(IContext ctx)
        {
            if (ctx.Message is string && i++ == 0)
            {
                capturedContext = ctx.Capture();

                throw new Exception("Test");
            }

            messages.Enqueue(ctx.Message!);

            if (ctx.Message is Started && capturedContext != null)
            {
                await capturedContext.Receive();
            }

            await Task.Yield();
        }

        var pid = context.Spawn(
            Props.FromFunc(ctx => ctx.Message switch
                {
                    object => HandleMessage(ctx),
                    _      => Task.CompletedTask
                }
            )
        );

        context.Send(pid, "hello");
        context.Send(pid, "hello");

        await context.PoisonAsync(pid);

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

    [Fact]
    public async Task StopActorWithLongRunningTask()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;
        var messages = new Queue<object>();

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
                        messages.Enqueue(e);
                    }
                }

                messages.Enqueue(ctx.Message!);
            })
        );

        context.Send(pid, "hello");
        // Wait a little while the actor starts to process the message//
        await Task.Delay(15);
        await context.StopAsync(pid);

        Assert.Equal(5, messages.Count);
        var msgs = messages.ToArray();
        Assert.IsType<Started>(msgs[0]);
        Assert.IsType<TaskCanceledException>(msgs[1]);
        Assert.IsType<string>(msgs[2]);
        Assert.IsType<Stopping>(msgs[3]);
        Assert.IsType<Stopped>(msgs[4]);
    }

    [Fact]
    public async Task ForwardActorAsync()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;

        PID SpawnForwarderFromFunc(Receive forwarder) => context.Spawn(Props.FromFunc(forwarder));

        PID SpawnActorFromFunc(Receive receive) => context.Spawn(Props.FromFunc(receive));

        var pid = SpawnActorFromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.Respond("hey");
                }

                return Task.CompletedTask;
            }
        );

        var forwarder = SpawnForwarderFromFunc(ctx =>
            {
                if (ctx.Message is string)
                {
                    ctx.Forward(pid);
                }

                return Task.CompletedTask;
            }
        );

        var reply = await context.RequestAsync<object>(forwarder, "hello");

        Assert.Equal("hey", reply);
    }
}