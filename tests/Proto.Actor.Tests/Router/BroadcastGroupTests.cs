using System;
using System.Threading.Tasks;
using Proto.Router.Messages;
using Proto.TestKit;
using Xunit;

namespace Proto.Router.Tests;

public class BroadcastGroupTests
{
    private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(1000);

    [Fact]
    public async Task BroadcastGroupRouter_AllRouteesReceiveMessages()
    {
        await using var system = new ActorSystem();

        var (router, routee1, routee2, routee3, probe1, probe2, probe3) =
            CreateBroadcastGroupRouterWith3Routees(system);

        system.Root.Send(router, "hello");

        await probe1.ExpectNextUserMessageAsync<string>(x => x == "hello");
        await probe2.ExpectNextUserMessageAsync<string>(x => x == "hello");
        await probe3.ExpectNextUserMessageAsync<string>(x => x == "hello");
    }

    [Fact]
    public async Task BroadcastGroupRouter_WhenOneRouteeIsStopped_AllOtherRouteesReceiveMessages()
    {
        await using var system = new ActorSystem();

        var (router, routee1, routee2, routee3, probe1, _, probe3) =
            CreateBroadcastGroupRouterWith3Routees(system);

        await system.Root.StopAsync(routee2);
        system.Root.Send(router, "hello");

        await probe1.ExpectNextUserMessageAsync<string>(x => x == "hello");
        await probe3.ExpectNextUserMessageAsync<string>(x => x == "hello");
    }

    [Fact]
    public async Task BroadcastGroupRouter_WhenOneRouteeIsSlow_AllOtherRouteesReceiveMessages()
    {
        await using var system = new ActorSystem();

        var (router, routee1, routee2, routee3, probe1, probe2, probe3) =
            CreateBroadcastGroupRouterWith3Routees(system,
                p => Props.FromProducer(() => new SlowForwardActor(p)));

        system.Root.Send(routee2, "go slow");
        system.Root.Send(router, "hello");

        await probe1.ExpectNextUserMessageAsync<string>(x => x == "hello");
        await probe3.ExpectNextUserMessageAsync<string>(x => x == "hello");
    }

    [Fact]
    public async Task BroadcastGroupRouter_RouteesCanBeRemoved()
    {
        await using var system = new ActorSystem();

        var (router, routee1, routee2, routee3, _, _, _) = CreateBroadcastGroupRouterWith3Routees(system);

        system.Root.Send(router, new RouterRemoveRoutee(routee1));

        var routees = await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
        Assert.DoesNotContain(routee1, routees.Pids);
        Assert.Contains(routee2, routees.Pids);
        Assert.Contains(routee3, routees.Pids);
    }

    [Fact]
    public async Task BroadcastGroupRouter_RouteesCanBeAdded()
    {
        await using var system = new ActorSystem();

        var (router, routee1, routee2, routee3, _, _, _) = CreateBroadcastGroupRouterWith3Routees(system);
        var (probe4, routee4) = system.CreateTestProbe();
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
        await using var system = new ActorSystem();

        var (router, routee1, routee2, routee3, probe1, probe2, probe3) =
            CreateBroadcastGroupRouterWith3Routees(system);

        system.Root.Send(router, "first message");
        system.Root.Send(router, new RouterRemoveRoutee(routee1));
        await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
        system.Root.Send(router, "second message");

        await probe1.ExpectNextUserMessageAsync<string>(x => x == "first message");
        await probe2.ExpectNextUserMessageAsync<string>(x => x == "first message");
        await probe3.ExpectNextUserMessageAsync<string>(x => x == "first message");
        await probe1.ExpectEmptyMailboxAsync(_timeout);
        await probe2.ExpectNextUserMessageAsync<string>(x => x == "second message");
        await probe3.ExpectNextUserMessageAsync<string>(x => x == "second message");
    }

    [Fact]
    public async Task BroadcastGroupRouter_AddedRouteesReceiveMessages()
    {
        await using var system = new ActorSystem();

        var (router, routee1, routee2, routee3, probe1, probe2, probe3) =
            CreateBroadcastGroupRouterWith3Routees(system);
        var (probe4, routee4) = system.CreateTestProbe();
        system.Root.Send(router, new RouterAddRoutee(routee4));
        await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
        system.Root.Send(router, "a message");

        await probe1.ExpectNextUserMessageAsync<string>(x => x == "a message");
        await probe2.ExpectNextUserMessageAsync<string>(x => x == "a message");
        await probe3.ExpectNextUserMessageAsync<string>(x => x == "a message");
        await probe4.ExpectNextUserMessageAsync<string>(x => x == "a message");
    }

    [Fact]
    public async Task BroadcastGroupRouter_AllRouteesReceiveRouterBroadcastMessages()
    {
        await using var system = new ActorSystem();

        var (router, routee1, routee2, routee3, probe1, probe2, probe3) =
            CreateBroadcastGroupRouterWith3Routees(system);

        system.Root.Send(router, new RouterBroadcastMessage("hello"));
        await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);

        await probe1.ExpectNextUserMessageAsync<string>(x => x == "hello");
        await probe2.ExpectNextUserMessageAsync<string>(x => x == "hello");
        await probe3.ExpectNextUserMessageAsync<string>(x => x == "hello");
    }

    private static (PID router, PID routee1, PID routee2, PID routee3, TestProbe probe1, TestProbe probe2,
        TestProbe probe3) CreateBroadcastGroupRouterWith3Routees(ActorSystem system,
        Func<TestProbe, Props>? routee2PropsFactory = null)
    {
        var (probe1, routee1) = system.CreateTestProbe();

        var (probe2, probe2Pid) = system.CreateTestProbe();
        var routee2 = routee2PropsFactory is null
            ? probe2Pid
            : system.Root.Spawn(routee2PropsFactory(probe2));

        var (probe3, routee3) = system.CreateTestProbe();

        var props = system.Root.NewBroadcastGroup(routee1, routee2, routee3);

        var router = system.Root.Spawn(props);

        return (router, routee1, routee2, routee3, probe1, probe2, probe3);
    }

    private class SlowForwardActor : IActor
    {
        private readonly TestProbe _probe;

        public SlowForwardActor(TestProbe probe) => _probe = probe;

        public async Task ReceiveAsync(IContext context)
        {
            if (context.Message is string s && s == "go slow")
            {
                await Task.Delay(5000);
                return;
            }

            context.Send(_probe.Self, context.Message);
        }
    }
}