using System;
using System.Threading.Tasks;
using Proto.Router.Messages;
using Proto.TestFixtures;
using Proto.TestKit;
using Xunit;

namespace Proto.Router.Tests;

public class RandomGroupRouterTests
{
    private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(1000);

    [Fact]
    public async Task RandomGroupRouter_RouteesReceiveMessagesInRandomOrder()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var (router, probe1, probe2, probe3) = CreateRouterWith3Routees(system);

        system.Root.Send(router, "1");
        system.Root.Send(router, "2");
        system.Root.Send(router, "3");

        await probe1.ExpectNextUserMessageAsync<string>(x => x == "2");
        await probe2.ExpectNextUserMessageAsync<string>(x => x == "3");
        await probe3.ExpectNextUserMessageAsync<string>(x => x == "1");
    }

    [Fact]
    public async Task RandomGroupRouter_NewlyAddedRouteesReceiveMessages()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var (router, probe1, probe2, probe3) = CreateRouterWith3Routees(system);
        var (probe4, routee4) = system.CreateTestProbe();
        system.Root.Send(router, new RouterAddRoutee(routee4));
        // Give router time to register the new routee
        await Task.Delay(500);
        for (var i = 0; i < 100; i++)
        {
            system.Root.Send(router, i.ToString());
        }

        await probe1.ExpectNextUserMessageAsync<string>();
        await probe3.ExpectNextUserMessageAsync<string>();
        await probe4.ExpectNextUserMessageAsync<string>();
    }

    [Fact]
    public async Task RandomGroupRouter_RemovedRouteesDoNotReceiveMessages()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var (router, probe1, _, _) = CreateRouterWith3Routees(system);

        system.Root.Send(router, new RouterRemoveRoutee(probe1.Self));

        await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);

        for (var i = 0; i < 100; i++)
        {
            system.Root.Send(router, i.ToString());
        }

        await probe1.ExpectEmptyMailboxAsync(_timeout);
    }

    [Fact]
    public async Task RandomGroupRouter_RouteesCanBeRemoved()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var (router, routee1, routee2, routee3) = CreateRouterWith3Routees(system);

        system.Root.Send(router, new RouterRemoveRoutee(routee1.Self));

        var routees = await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
        Assert.DoesNotContain(routee1.Self, routees.Pids);
        Assert.Contains(routee2.Self, routees.Pids);
        Assert.Contains(routee3.Self, routees.Pids);
    }

    [Fact]
    public async Task RandomGroupRouter_RouteesCanBeAdded()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var (router, routee1, routee2, routee3) = CreateRouterWith3Routees(system);
        var (probe4, routee4) = system.CreateTestProbe();
        system.Root.Send(router, new RouterAddRoutee(routee4));

        var routees = await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
        Assert.Contains(routee1.Self, routees.Pids);
        Assert.Contains(routee2.Self, routees.Pids);
        Assert.Contains(routee3.Self, routees.Pids);
        Assert.Contains(routee4, routees.Pids);
    }

    [Fact]
    public async Task RandomGroupRouter_AllRouteesReceiveRouterBroadcastMessages()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var (router, probe1, probe2, probe3) = CreateRouterWith3Routees(system);

        system.Root.Send(router, new RouterBroadcastMessage("hello"));

        await probe1.ExpectNextUserMessageAsync<string>(x => x == "hello");
        await probe2.ExpectNextUserMessageAsync<string>(x => x == "hello");
        await probe3.ExpectNextUserMessageAsync<string>(x => x == "hello");
    }
    private (PID router, TestProbe routee1, TestProbe routee2, TestProbe routee3) CreateRouterWith3Routees(ActorSystem system)
    {
        var (probe1, pid1) = system.CreateTestProbe();
        var (probe2, pid2) = system.CreateTestProbe();
        var (probe3, pid3) = system.CreateTestProbe();

        var props = system.Root.NewRandomGroup(10000, pid1, pid2, pid3);

        var router = system.Root.Spawn(props);

        return (router, probe1, probe2, probe3);
    }
}