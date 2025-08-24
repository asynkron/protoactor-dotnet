using System;
using System.Threading.Tasks;
using Proto.Router.Messages;
using Proto.TestFixtures;
using Proto.TestKit;
using Xunit;

namespace Proto.Router.Tests;

public class RoundRobinGroupTests
{
    private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(1000);

    [Fact]
    public async Task RoundRobinGroupRouter_RouteesReceiveMessagesInRoundRobinStyle()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var (router, probe1, probe2, probe3) = CreateRoundRobinRouterWith3Routees(system);

        system.Root.Send(router, "1");
        await probe1.ExpectNextUserMessageAsync<string>(x => x == "1");
        await probe2.ExpectEmptyMailboxAsync(_timeout);
        await probe3.ExpectEmptyMailboxAsync(_timeout);

        system.Root.Send(router, "2");
        await probe2.ExpectNextUserMessageAsync<string>(x => x == "2");
        await probe1.ExpectEmptyMailboxAsync(_timeout);
        await probe3.ExpectEmptyMailboxAsync(_timeout);

        system.Root.Send(router, "3");
        await probe3.ExpectNextUserMessageAsync<string>(x => x == "3");
        await probe1.ExpectEmptyMailboxAsync(_timeout);
        await probe2.ExpectEmptyMailboxAsync(_timeout);

        system.Root.Send(router, "4");
        await probe1.ExpectNextUserMessageAsync<string>(x => x == "4");
        await probe2.ExpectEmptyMailboxAsync(_timeout);
        await probe3.ExpectEmptyMailboxAsync(_timeout);
    }

    [Fact]
    public async Task RoundRobinGroupRouter_RouteesCanBeRemoved()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var (router, probe1, probe2, probe3) = CreateRoundRobinRouterWith3Routees(system);
        var initial = await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
        var pid1 = initial.Pids[0];
        var pid2 = initial.Pids[1];
        var pid3 = initial.Pids[2];

        system.Root.Send(router, new RouterRemoveRoutee(pid1));

        var routees = await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
        Assert.DoesNotContain(pid1, routees.Pids);
        Assert.Contains(pid2, routees.Pids);
        Assert.Contains(pid3, routees.Pids);
    }

    [Fact]
    public async Task RoundRobinGroupRouter_RouteesCanBeAdded()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var (router, probe1, probe2, probe3) = CreateRoundRobinRouterWith3Routees(system);
        var initial = await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
        var pid1 = initial.Pids[0];
        var pid2 = initial.Pids[1];
        var pid3 = initial.Pids[2];
        var (probe4, routee4) = system.CreateTestProbe();
        system.Root.Send(router, new RouterAddRoutee(routee4));

        var routees = await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
        Assert.Contains(pid1, routees.Pids);
        Assert.Contains(pid2, routees.Pids);
        Assert.Contains(pid3, routees.Pids);
        Assert.Contains(routee4, routees.Pids);
    }

    [Fact]
    public async Task RoundRobinGroupRouter_RemovedRouteesNoLongerReceiveMessages()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var (router, probe1, probe2, probe3) = CreateRoundRobinRouterWith3Routees(system);

        system.Root.Send(router, "0");
        await probe1.ExpectNextUserMessageAsync<string>(x => x == "0");
        system.Root.Send(router, "0");
        await probe2.ExpectNextUserMessageAsync<string>(x => x == "0");
        system.Root.Send(router, "0");
        await probe3.ExpectNextUserMessageAsync<string>(x => x == "0");
        system.Root.Send(router, new RouterRemoveRoutee(probe1.Self));
        await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);

        system.Root.Send(router, "3");
        await probe3.ExpectNextUserMessageAsync<string>(x => x == "3");
        system.Root.Send(router, "3");
        await probe2.ExpectNextUserMessageAsync<string>(x => x == "3");
        system.Root.Send(router, "3");
        await probe3.ExpectNextUserMessageAsync<string>(x => x == "3");

        await probe1.ExpectEmptyMailboxAsync(_timeout);
    }

    [Fact]
    public async Task RoundRobinGroupRouter_AddedRouteesReceiveMessages()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var (router, probe1, probe2, probe3) = CreateRoundRobinRouterWith3Routees(system);
        var (probe4, routee4) = system.CreateTestProbe();
        system.Root.Send(router, new RouterAddRoutee(routee4));
        await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
        system.Root.Send(router, "1");
        await probe1.ExpectNextUserMessageAsync<string>(x => x == "1");
        system.Root.Send(router, "1");
        await probe2.ExpectNextUserMessageAsync<string>(x => x == "1");
        system.Root.Send(router, "1");
        await probe3.ExpectNextUserMessageAsync<string>(x => x == "1");
        system.Root.Send(router, "1");
        await probe4.ExpectNextUserMessageAsync<string>(x => x == "1");
    }

    [Fact]
    public async Task RoundRobinGroupRouter_AllRouteesReceiveRouterBroadcastMessages()
    {
        await using var system = new ActorSystem();

        var (probe1, routee1) = system.CreateTestProbe();
        var (probe2, routee2) = system.CreateTestProbe();
        var (probe3, routee3) = system.CreateTestProbe();

        var props = system.Root.NewRoundRobinGroup(routee1, routee2, routee3);
        var router = system.Root.Spawn(props);

        system.Root.Send(router, new RouterBroadcastMessage("hello"));

        await probe1.ExpectNextUserMessageAsync<string>(x => x == "hello");
        await probe2.ExpectNextUserMessageAsync<string>(x => x == "hello");
        await probe3.ExpectNextUserMessageAsync<string>(x => x == "hello");
    }

    private static (PID router, TestProbe routee1, TestProbe routee2, TestProbe routee3) CreateRoundRobinRouterWith3Routees(ActorSystem system)
    {
        var (probe1, pid1) = system.CreateTestProbe();
        var (probe2, pid2) = system.CreateTestProbe();
        var (probe3, pid3) = system.CreateTestProbe();

        var props = system.Root.NewRoundRobinGroup(pid1, pid2, pid3);
        var router = system.Root.Spawn(props);

        return (router, probe1, probe2, probe3);
    }
}
