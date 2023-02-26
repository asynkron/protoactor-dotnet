using System;
using System.Threading.Tasks;
using Proto.Router.Messages;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Router.Tests;

public class BroadcastGroupTests
{
    private static readonly Props MyActorProps = Props.FromProducer(() => new MyTestActor());
    private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(1000);

    [Fact]
    public async Task BroadcastGroupRouter_AllRouteesReceiveMessages()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees(system);

        system.Root.Send(router, "hello");

        Assert.Equal("hello", await system.Root.RequestAsync<string>(routee1, "received?", _timeout));
        Assert.Equal("hello", await system.Root.RequestAsync<string>(routee2, "received?", _timeout));
        Assert.Equal("hello", await system.Root.RequestAsync<string>(routee3, "received?", _timeout));
    }

    [Fact]
    public async Task BroadcastGroupRouter_WhenOneRouteeIsStopped_AllOtherRouteesReceiveMessages()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees(system);

        await system.Root.StopAsync(routee2);
        system.Root.Send(router, "hello");

        Assert.Equal("hello", await system.Root.RequestAsync<string>(routee1, "received?", _timeout));
        Assert.Equal("hello", await system.Root.RequestAsync<string>(routee3, "received?", _timeout));
    }

    [Fact]
    public async Task BroadcastGroupRouter_WhenOneRouteeIsSlow_AllOtherRouteesReceiveMessages()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees(system);

        system.Root.Send(routee2, "go slow");
        system.Root.Send(router, "hello");

        Assert.Equal("hello", await system.Root.RequestAsync<string>(routee1, "received?", _timeout));
        Assert.Equal("hello", await system.Root.RequestAsync<string>(routee3, "received?", _timeout));
    }

    [Fact]
    public async Task BroadcastGroupRouter_RouteesCanBeRemoved()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees(system);

        system.Root.Send(router, new RouterRemoveRoutee(routee1));

        var routees = await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
        Assert.DoesNotContain(routee1, routees.Pids);
        Assert.Contains(routee2, routees.Pids);
        Assert.Contains(routee3, routees.Pids);
    }

    [Fact]
    public async Task BroadcastGroupRouter_RouteesCanBeAdded()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees(system);
        var routee4 = system.Root.Spawn(MyActorProps);
        system.Root.Send(router, new RouterAddRoutee(routee4));

        var routees = await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
        Assert.Contains(routee1, routees.Pids);
        Assert.Contains(routee2, routees.Pids);
        Assert.Contains(routee3, routees.Pids);
        Assert.Contains(routee4, routees.Pids);
    }

    [Fact]
    public async Task BroadcastGroupRouter_RemovedRouteesNoLongerReceiveMessages()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees(system);

        system.Root.Send(router, "first message");
        system.Root.Send(router, new RouterRemoveRoutee(routee1));
        system.Root.Send(router, "second message");

        Assert.Equal("first message", await system.Root.RequestAsync<string>(routee1, "received?", _timeout));
        Assert.Equal("second message", await system.Root.RequestAsync<string>(routee2, "received?", _timeout));
        Assert.Equal("second message", await system.Root.RequestAsync<string>(routee3, "received?", _timeout));
    }

    [Fact]
    public async Task BroadcastGroupRouter_AddedRouteesReceiveMessages()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees(system);
        var routee4 = system.Root.Spawn(MyActorProps);
        system.Root.Send(router, new RouterAddRoutee(routee4));
        system.Root.Send(router, "a message");

        Assert.Equal("a message", await system.Root.RequestAsync<string>(routee1, "received?", _timeout));
        Assert.Equal("a message", await system.Root.RequestAsync<string>(routee2, "received?", _timeout));
        Assert.Equal("a message", await system.Root.RequestAsync<string>(routee3, "received?", _timeout));
        Assert.Equal("a message", await system.Root.RequestAsync<string>(routee4, "received?", _timeout));
    }

    [Fact]
    public async Task BroadcastGroupRouter_AllRouteesReceiveRouterBroadcastMessages()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees(system);

        system.Root.Send(router, new RouterBroadcastMessage("hello"));

        Assert.Equal("hello", await system.Root.RequestAsync<string>(routee1, "received?", _timeout));
        Assert.Equal("hello", await system.Root.RequestAsync<string>(routee2, "received?", _timeout));
        Assert.Equal("hello", await system.Root.RequestAsync<string>(routee3, "received?", _timeout));
    }

    private static (PID router, PID routee1, PID routee2, PID routee3) CreateBroadcastGroupRouterWith3Routees(
        ActorSystem system)
    {
        var routee1 = system.Root.Spawn(MyActorProps);
        var routee2 = system.Root.Spawn(MyActorProps);
        var routee3 = system.Root.Spawn(MyActorProps);

        var props = system.Root.NewBroadcastGroup(routee1, routee2, routee3)
            .WithMailbox(() => new TestMailbox());

        var router = system.Root.Spawn(props);

        return (router, routee1, routee2, routee3);
    }

    private class MyTestActor : IActor
    {
        private string? _received;

        public async Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case "received?":
                    context.Respond(_received!);

                    break;
                case "go slow":
                    await Task.Delay(5000);

                    break;
                case string msg:
                    _received = msg;

                    break;
            }
        }
    }
}