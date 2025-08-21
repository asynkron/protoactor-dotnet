using System;
using System.Threading.Tasks;
using Proto;
using Proto.TestFixtures;
using Proto.TestKit;
using Xunit;

namespace Proto.Tests;

public class WatchTests
{
    [Fact]
    public async Task MultipleStopsTriggerSingleTerminated()
    {
        await using var system = new ActorSystem();
        var context = system.Root;

        var probe = new TestProbe();
        var probePid = context.Spawn(Props.FromProducer(() => probe));

        // child stops itself twice when receiving "stop"
        var childProps = Props.FromFunc(ctx =>
        {
            if (ctx.Message is "stop")
            {
                ctx.Stop(ctx.Self);
                ctx.Stop(ctx.Self);
            }

            return Task.CompletedTask;
        });

        var child = context.Spawn(childProps);

        // register probe as watcher for the child
        child.SendSystemMessage(system, new Watch(probePid));

        context.Send(child, "stop");

        await probe.GetNextMessageAsync<Terminated>();
        await probe.ExpectNoMessageAsync(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task CanWatchLocalActors()
    {
        await using var system = new ActorSystem();
        var context = system.Root;

        var watchee = context.Spawn(Props.FromProducer(() => new DoNothingActor()));

        var probe = new TestProbe();
        var probePid = context.Spawn(Props.FromProducer(() => probe));

        watchee.SendSystemMessage(system, new Watch(probePid));

        await context.StopAsync(watchee);

        await probe.GetNextMessageAsync<Terminated>();
    }

    [Fact]
    public async Task UnwatchPreventsTerminatedMessage()
    {
        await using var system = new ActorSystem();
        var context = system.Root;

        var watchee = context.Spawn(Props.FromProducer(() => new DoNothingActor()));

        var probe = new TestProbe();
        var probePid = context.Spawn(Props.FromProducer(() => probe));

        watchee.SendSystemMessage(system, new Watch(probePid));
        watchee.SendSystemMessage(system, new Unwatch(probePid));

        await context.StopAsync(watchee);

        await probe.ExpectNoMessageAsync(TimeSpan.FromMilliseconds(100));
    }
}