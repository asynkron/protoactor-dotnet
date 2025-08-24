using System;
using System.Threading.Tasks;
using Proto.Router.Messages;
using Proto.TestFixtures;
using Proto.TestKit;
using Xunit;

namespace Proto.Router.Tests;

public class ConsistentHashGroupTests
{
    private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(1000);

    [Fact]
    public async Task ConsistentHashGroupRouter_MessageWithSameHashAlwaysGoesToSameRoutee()
    {
        await using var system = new ActorSystem();

        var (router, probe1, probe2, probe3) = CreateRouterWith3Routees(system);

        system.Root.Send(router, new Message("message1"));
        await probe1.ExpectNextUserMessageAsync<Message>(m => m.ToString() == "message1");
        system.Root.Send(router, new Message("message1"));
        await probe1.ExpectNextUserMessageAsync<Message>(m => m.ToString() == "message1");
        system.Root.Send(router, new Message("message1"));
        await probe1.ExpectNextUserMessageAsync<Message>(m => m.ToString() == "message1");

        await probe2.ExpectEmptyMailboxAsync(_timeout);
        await probe3.ExpectEmptyMailboxAsync(_timeout);
    }

    [Fact]
    public async Task ConsistentHashGroupRouter_with_MessageHasherFunc_MessageWithSameHashAlwaysGoesToSameRoutee()
    {
        await using var system = new ActorSystem();

        var (router, probe1, probe2, probe3) = CreateRouterWith3Routees(system, x => x.ToString()!);

        system.Root.Send(router, "message1");
        await probe1.ExpectNextUserMessageAsync<string>(x => x == "message1");
        system.Root.Send(router, "message1");
        await probe1.ExpectNextUserMessageAsync<string>(x => x == "message1");
        system.Root.Send(router, "message1");
        await probe1.ExpectNextUserMessageAsync<string>(x => x == "message1");

        await probe2.ExpectEmptyMailboxAsync(_timeout);
        await probe3.ExpectEmptyMailboxAsync(_timeout);
    }

    [Fact]
    public async Task ConsistentHashGroupRouter_MessagesWithDifferentHashesGoToDifferentRoutees()
    {
        await using var system = new ActorSystem();

        var (router, probe1, probe2, probe3) = CreateRouterWith3Routees(system);

        system.Root.Send(router, new Message("message1"));
        system.Root.Send(router, new Message("message2"));
        system.Root.Send(router, new Message("message3"));

        await probe1.ExpectNextUserMessageAsync<Message>(m => m.ToString() == "message1");
        await probe2.ExpectNextUserMessageAsync<Message>(m => m.ToString() == "message2");
        await probe3.ExpectNextUserMessageAsync<Message>(m => m.ToString() == "message3");
    }

    [Fact]
    public async Task ConsistentHashGroupRouter_MessageWithSameHashAlwaysGoesToSameRoutee_EvenWhenNewRouteeAdded()
    {
        await using var system = new ActorSystem();

        var (router, probe1, probe2, probe3) = CreateRouterWith3Routees(system);

        system.Root.Send(router, new Message("message1"));
        await probe1.ExpectNextUserMessageAsync<Message>(m => m.ToString() == "message1");

        var (probe4, routee4) = CreateNamedTestProbe(system, "routee4");
        system.Root.Send(router, new RouterAddRoutee(routee4));
        system.Root.Send(router, new Message("message1"));

        await probe1.ExpectNextUserMessageAsync<Message>(m => m.ToString() == "message1");
        await probe2.ExpectEmptyMailboxAsync(_timeout);
        await probe3.ExpectEmptyMailboxAsync(_timeout);
        await probe4.ExpectEmptyMailboxAsync(_timeout);
    }

    [Fact]
    public async Task ConsistentHashGroupRouter_RouteesCanBeRemoved()
    {
        await using var system = new ActorSystem();

        var (router, routee1, routee2, routee3) = CreateRouterWith3Routees(system);

        system.Root.Send(router, new RouterRemoveRoutee(routee1.Self));

        var routees = await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
        Assert.DoesNotContain(routee1.Self, routees.Pids);
        Assert.Contains(routee2.Self, routees.Pids);
        Assert.Contains(routee3.Self, routees.Pids);
    }

    [Fact]
    public async Task ConsistentHashGroupRouter_RouteesCanBeAdded()
    {
        await using var system = new ActorSystem();

        var (router, routee1, routee2, routee3) = CreateRouterWith3Routees(system);
        var (probe4, routee4) = CreateNamedTestProbe(system, "routee4");
        system.Root.Send(router, new RouterAddRoutee(routee4));

        var routees = await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
        Assert.Contains(routee1.Self, routees.Pids);
        Assert.Contains(routee2.Self, routees.Pids);
        Assert.Contains(routee3.Self, routees.Pids);
        Assert.Contains(routee4, routees.Pids);
    }

    [Fact]
    public async Task ConsistentHashGroupRouter_RemovedRouteesNoLongerReceiveMessages()
    {
        await using var system = new ActorSystem();

        var (router, probe1, _, _) = CreateRouterWith3Routees(system);

        system.Root.Send(router, new RouterRemoveRoutee(probe1.Self));
        await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
        system.Root.Send(router, new Message("message1"));
        await probe1.ExpectEmptyMailboxAsync(_timeout);
    }

    [Fact]
    public async Task ConsistentHashGroupRouter_AddedRouteesReceiveMessages()
    {
        await using var system = new ActorSystem();

        var (router, _, _, _) = CreateRouterWith3Routees(system);
        var (probe4, routee4) = CreateNamedTestProbe(system, "routee4");
        system.Root.Send(router, new RouterAddRoutee(routee4));
        await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
        system.Root.Send(router, new Message("message4"));
        await probe4.ExpectNextUserMessageAsync<Message>(m => m.ToString() == "message4");
    }

    [Fact]
    public async Task ConsistentHashGroupRouter_MessageIsReassignedWhenRouteeRemoved()
    {
        await using var system = new ActorSystem();

        var (router, probe1, probe2, _) = CreateRouterWith3Routees(system);

        system.Root.Send(router, new Message("message1"));
        await probe1.ExpectNextUserMessageAsync<Message>(m => m.ToString() == "message1");
        system.Root.Send(router, new RouterRemoveRoutee(probe1.Self));
        await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
        system.Root.Send(router, new Message("message1"));
        await probe1.ExpectEmptyMailboxAsync(_timeout);

        await probe2.ExpectNextUserMessageAsync<Message>(m => m.ToString() == "message1");
    }

    [Fact]
    public async Task ConsistentHashGroupRouter_AllRouteesReceiveRouterBroadcastMessages()
    {
        await using var system = new ActorSystem();

        var (router, probe1, probe2, probe3) = CreateRouterWith3Routees(system);

        system.Root.Send(router, new RouterBroadcastMessage(new Message("hello")));

        await probe1.ExpectNextUserMessageAsync<Message>(m => m.ToString() == "hello");
        await probe2.ExpectNextUserMessageAsync<Message>(m => m.ToString() == "hello");
        await probe3.ExpectNextUserMessageAsync<Message>(m => m.ToString() == "hello");
    }
    private static (PID router, TestProbe routee1, TestProbe routee2, TestProbe routee3) CreateRouterWith3Routees(
        ActorSystem system,
        Func<object, string>? messageHasher = null
    )
    {
        var (routee1, pid1) = CreateNamedTestProbe(system, "routee1");
        var (routee2, pid2) = CreateNamedTestProbe(system, "routee2");
        var (routee3, pid3) = CreateNamedTestProbe(system, "routee3");

        var props = system.Root.NewConsistentHashGroup(
            SuperIntelligentDeterministicHash.Hash,
            1,
            messageHasher,
            pid1,
            pid2,
            pid3
        );

        var router = system.Root.Spawn(props);

        return (router, routee1, routee2, routee3);
    }

    private static (TestProbe probe, PID pid) CreateNamedTestProbe(ActorSystem system, string name)
    {
        var probe = new TestProbe();
        var pid = system.Root.SpawnNamed(Props.FromProducer(() => probe), Guid.NewGuid() + name);
        return (probe, pid);
    }

    private static class SuperIntelligentDeterministicHash
    {
        public static uint Hash(string hashKey)
        {
            if (hashKey.EndsWith("routee1"))
            {
                return 10;
            }

            if (hashKey.EndsWith("routee2"))
            {
                return 20;
            }

            if (hashKey.EndsWith("routee3"))
            {
                return 30;
            }

            if (hashKey.EndsWith("routee4"))
            {
                return 40;
            }

            if (hashKey.EndsWith("message1"))
            {
                return 9;
            }

            if (hashKey.EndsWith("message2"))
            {
                return 19;
            }

            if (hashKey.EndsWith("message3"))
            {
                return 29;
            }

            if (hashKey.EndsWith("message4"))
            {
                return 39;
            }

            return 0;
        }
    }

    internal class Message : IHashable
    {
        private readonly string _value;

        public Message(string value)
        {
            _value = value;
        }

        public string HashBy() => _value;

        public override string ToString() => _value;
    }
}