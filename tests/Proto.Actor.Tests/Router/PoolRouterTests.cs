using System;
using System.Threading.Tasks;
using FluentAssertions;
using Proto.Router.Messages;
using Proto.Router.Routers;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Router.Tests;

public class PoolRouterTests
{
    private static readonly Props MyActorProps = Props.FromProducer(() => new DoNothingActor());
    private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(1000);

    [Fact]
    public async Task BroadcastGroupPool_CreatesRoutees()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var props = system.Root.NewBroadcastPool(MyActorProps, 3)
            .WithMailbox(() => new TestMailbox());

        var router = system.Root.Spawn(props);
        var routees = await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
        Assert.Equal(3, routees.Pids.Count);
    }

    [Fact]
    public async Task RoundRobinPool_CreatesRoutees()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var props = system.Root.NewRoundRobinPool(MyActorProps, 3)
            .WithMailbox(() => new TestMailbox());

        var router = system.Root.Spawn(props);
        var routees = await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
        Assert.Equal(3, routees.Pids.Count);
    }

    [Fact]
    public async Task ConsistentHashPool_CreatesRoutees()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var props = system.Root.NewConsistentHashPool(MyActorProps, 3)
            .WithMailbox(() => new TestMailbox());

        var router = system.Root.Spawn(props);
        var routees = await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
        Assert.Equal(3, routees.Pids.Count);
    }

    [Fact]
    public async Task RandomPool_CreatesRoutees()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var props = system.Root.NewRandomPool(MyActorProps, 3, 0)
            .WithMailbox(() => new TestMailbox());

        var router = system.Root.Spawn(props);
        var routees = await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
        Assert.Equal(3, routees.Pids.Count);
    }

    [Fact]
    public async Task If_routee_props_then_router_creation_fails()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var failingProps = Props.FromProducer(() => throw new Exception("Failing props"));

        system.Invoking(s => s.Root.Spawn(s.Root.NewRandomPool(failingProps, 3, 0)))
            .Should()
            .Throw<RouterStartFailedException>()
            .WithInnerException<Exception>()
            .WithMessage("Failing props");
    }
}