using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Proto.Router;
using Proto.Router.Messages;
using Proto.Router.Routers;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Router.Tests;

public class PoolRouterTests
{
    private static readonly Props MyActorProps = Props.FromProducer(() => new DoNothingActor());
    private static readonly Props TrackingActorProps = Props.FromProducer(() => new MyTestActor());
    private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(1000);

    [Fact]
    public async Task BroadcastGroupPool_CreatesRoutees()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var props = system.Root.NewBroadcastPool(MyActorProps, 3);

        var router = system.Root.Spawn(props);
        var routees = await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
        Assert.Equal(3, routees.Pids.Count);
    }

    [Fact]
    public async Task RoundRobinPool_CreatesRoutees()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var props = system.Root.NewRoundRobinPool(MyActorProps, 3);

        var router = system.Root.Spawn(props);
        var routees = await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
        Assert.Equal(3, routees.Pids.Count);
    }

    [Fact]
    public async Task ConsistentHashPool_CreatesRoutees()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var props = system.Root.NewConsistentHashPool(MyActorProps, 3);

        var router = system.Root.Spawn(props);
        var routees = await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
        Assert.Equal(3, routees.Pids.Count);
    }

    [Fact]
    public async Task RandomPool_CreatesRoutees()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var props = system.Root.NewRandomPool(MyActorProps, 3, 0);

        var router = system.Root.Spawn(props);
        var routees = await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
        Assert.Equal(3, routees.Pids.Count);
    }

    [Fact]
    public async Task BroadcastPoolRouter_BroadcastsMessagesToAllRoutees()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var props = system.Root.NewBroadcastPool(TrackingActorProps, 3);

        var router = system.Root.Spawn(props);

        system.Root.Send(router, "hello");

        var routees = await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);

        foreach (var pid in routees.Pids)
        {
            Assert.Equal(1, await system.Root.RequestAsync<int>(pid, "received?", _timeout));
        }
    }

    [Fact]
    public async Task RoundRobinPoolRouter_DistributesMessagesInRoundRobinStyle()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var props = system.Root.NewRoundRobinPool(TrackingActorProps, 3);

        var router = system.Root.Spawn(props);

        system.Root.Send(router, "1");
        system.Root.Send(router, "2");
        system.Root.Send(router, "3");
        system.Root.Send(router, "4");

        var routees = await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);

        var counts = new List<int>();

        foreach (var pid in routees.Pids)
        {
            counts.Add(await system.Root.RequestAsync<int>(pid, "received?", _timeout));
        }

        counts.Sort();

        Assert.Equal(new[] {1, 1, 2}, counts);
    }

    [Fact]
    public async Task ConsistentHashPoolRouter_MessageWithSameHashAlwaysGoesToSameRoutee()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var props = system.Root.NewConsistentHashPool(TrackingActorProps, 3);

        var router = system.Root.Spawn(props);

        system.Root.Send(router, new Message("message1"));
        system.Root.Send(router, new Message("message1"));
        system.Root.Send(router, new Message("message1"));

        var routees = await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);

        var counts = new List<int>();

        foreach (var pid in routees.Pids)
        {
            counts.Add(await system.Root.RequestAsync<int>(pid, "received?", _timeout));
        }

        counts.Sort();

        Assert.Equal(new[] {0, 0, 3}, counts);
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

    private class MyTestActor : IActor
    {
        private readonly List<string> _receivedMessages = new();

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case string msg when msg == "received?":
                    context.Respond(_receivedMessages.Count);
                    break;
                case Message msg:
                    _receivedMessages.Add(msg.ToString());
                    break;
                case string msg:
                    _receivedMessages.Add(msg);
                    break;
            }

            return Task.CompletedTask;
        }
    }

    private class Message : IHashable
    {
        private readonly string _value;

        public Message(string value) => _value = value;

        public string HashBy() => _value;

        public override string ToString() => _value;
    }
}

