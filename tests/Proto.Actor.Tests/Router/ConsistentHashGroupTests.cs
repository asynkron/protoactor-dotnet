using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Proto.Router.Messages;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Router.Tests
{
    public class ConsistentHashGroupTests
    {
        private static readonly Props MyActorProps = Props.FromProducer(() => new MyTestActor())
            .WithMailbox(() => new TestMailbox());

        private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(1000);

        [Fact]
        public async Task ConsistentHashGroupRouter_MessageWithSameHashAlwaysGoesToSameRoutee()
        {
            await using var system = new ActorSystem();

            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees(system);

            system.Root.Send(router, new Message("message1"));
            system.Root.Send(router, new Message("message1"));
            system.Root.Send(router, new Message("message1"));

            Assert.Equal(3, await system.Root.RequestAsync<int>(routee1, "received?", _timeout));
            Assert.Equal(0, await system.Root.RequestAsync<int>(routee2, "received?", _timeout));
            Assert.Equal(0, await system.Root.RequestAsync<int>(routee3, "received?", _timeout));
        }

        [Fact]
        public async Task ConsistentHashGroupRouter_with_MessageHasherFunc_MessageWithSameHashAlwaysGoesToSameRoutee()
        {
            await using var system = new ActorSystem();

            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees(system, x => x.ToString()!);

            system.Root.Send(router, "message1");
            system.Root.Send(router, "message1");
            system.Root.Send(router, "message1");

            Assert.Equal(3, await system.Root.RequestAsync<int>(routee1, "received?", _timeout));
            Assert.Equal(0, await system.Root.RequestAsync<int>(routee2, "received?", _timeout));
            Assert.Equal(0, await system.Root.RequestAsync<int>(routee3, "received?", _timeout));
        }

        [Fact]
        public async Task ConsistentHashGroupRouter_MessagesWithDifferentHashesGoToDifferentRoutees()
        {
            await using var system = new ActorSystem();

            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees(system);

            system.Root.Send(router, new Message("message1"));
            system.Root.Send(router, new Message("message2"));
            system.Root.Send(router, new Message("message3"));

            Assert.Equal(1, await system.Root.RequestAsync<int>(routee1, "received?", _timeout));
            Assert.Equal(1, await system.Root.RequestAsync<int>(routee2, "received?", _timeout));
            Assert.Equal(1, await system.Root.RequestAsync<int>(routee3, "received?", _timeout));
        }

        [Fact]
        public async Task ConsistentHashGroupRouter_MessageWithSameHashAlwaysGoesToSameRoutee_EvenWhenNewRouteeAdded()
        {
            await using var system = new ActorSystem();

            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees(system);

            system.Root.Send(router, new Message("message1"));
            var routee4 = system.Root.Spawn(MyActorProps);
            system.Root.Send(router, new RouterAddRoutee(routee4));
            system.Root.Send(router, new Message("message1"));

            Assert.Equal(2, await system.Root.RequestAsync<int>(routee1, "received?", _timeout));
            Assert.Equal(0, await system.Root.RequestAsync<int>(routee2, "received?", _timeout));
            Assert.Equal(0, await system.Root.RequestAsync<int>(routee3, "received?", _timeout));
        }

        [Fact]
        public async Task ConsistentHashGroupRouter_RouteesCanBeRemoved()
        {
            await using var system = new ActorSystem();

            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees(system);

            system.Root.Send(router, new RouterRemoveRoutee(routee1));

            var routees = await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.DoesNotContain(routee1, routees.Pids);
            Assert.Contains(routee2, routees.Pids);
            Assert.Contains(routee3, routees.Pids);
        }

        [Fact]
        public async Task ConsistentHashGroupRouter_RouteesCanBeAdded()
        {
            await using var system = new ActorSystem();

            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees(system);
            var routee4 = system.Root.Spawn(MyActorProps);
            system.Root.Send(router, new RouterAddRoutee(routee4));

            var routees = await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.Contains(routee1, routees.Pids);
            Assert.Contains(routee2, routees.Pids);
            Assert.Contains(routee3, routees.Pids);
            Assert.Contains(routee4, routees.Pids);
        }

        [Fact]
        public async Task ConsistentHashGroupRouter_RemovedRouteesNoLongerReceiveMessages()
        {
            await using var system = new ActorSystem();

            var (router, routee1, _, _) = CreateRouterWith3Routees(system);

            system.Root.Send(router, new RouterRemoveRoutee(routee1));
            system.Root.Send(router, new Message("message1"));
            Assert.Equal(0, await system.Root.RequestAsync<int>(routee1, "received?", _timeout));
        }

        [Fact]
        public async Task ConsistentHashGroupRouter_AddedRouteesReceiveMessages()
        {
            await using var system = new ActorSystem();

            var (router, _, _, _) = CreateRouterWith3Routees(system);
            var routee4 = system.Root.Spawn(MyActorProps);
            system.Root.Send(router, new RouterAddRoutee(routee4));
            system.Root.Send(router, new Message("message4"));
            Assert.Equal(1, await system.Root.RequestAsync<int>(routee4, "received?", _timeout));
        }

        [Fact]
        public async Task ConsistentHashGroupRouter_MessageIsReassignedWhenRouteeRemoved()
        {
            await using var system = new ActorSystem();

            var (router, routee1, routee2, _) = CreateRouterWith3Routees(system);

            system.Root.Send(router, new Message("message1"));
            // routee1 handles "message1"
            Assert.Equal(1, await system.Root.RequestAsync<int>(routee1, "received?", _timeout));
            // remove receiver
            system.Root.Send(router, new RouterRemoveRoutee(routee1));
            // routee2 should now handle "message1"
            system.Root.Send(router, new Message("message1"));

            Assert.Equal(1, await system.Root.RequestAsync<int>(routee2, "received?", _timeout));
        }

        [Fact]
        public async Task ConsistentHashGroupRouter_AllRouteesReceiveRouterBroadcastMessages()
        {
            await using var system = new ActorSystem();

            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees(system);

            system.Root.Send(router, new RouterBroadcastMessage(new Message("hello")));

            Assert.Equal(1, await system.Root.RequestAsync<int>(routee1, "received?", _timeout));
            Assert.Equal(1, await system.Root.RequestAsync<int>(routee2, "received?", _timeout));
            Assert.Equal(1, await system.Root.RequestAsync<int>(routee3, "received?", _timeout));
        }

        private static (PID router, PID routee1, PID routee2, PID routee3) CreateRouterWith3Routees(
            ActorSystem system,
            Func<object, string>? messageHasher = null
        )
        {
            // assign unique names for when tests run in parallel
            var routee1 = system.Root.SpawnNamed(MyActorProps, Guid.NewGuid() + "routee1");
            var routee2 = system.Root.SpawnNamed(MyActorProps, Guid.NewGuid() + "routee2");
            var routee3 = system.Root.SpawnNamed(MyActorProps, Guid.NewGuid() + "routee3");

            var props = system.Root.NewConsistentHashGroup(SuperIntelligentDeterministicHash.Hash, 1, messageHasher,
                    routee1, routee2, routee3
                )
                .WithMailbox(() => new TestMailbox());
            var router = system.Root.Spawn(props);
            return (router, routee1, routee2, routee3);
        }

        private static class SuperIntelligentDeterministicHash
        {
            public static uint Hash(string hashKey)
            {
                if (hashKey.EndsWith("routee1")) return 10;
                if (hashKey.EndsWith("routee2")) return 20;
                if (hashKey.EndsWith("routee3")) return 30;
                if (hashKey.EndsWith("routee4")) return 40;
                if (hashKey.EndsWith("message1")) return 9;
                if (hashKey.EndsWith("message2")) return 19;
                if (hashKey.EndsWith("message3")) return 29;
                if (hashKey.EndsWith("message4")) return 39;

                return 0;
            }
        }

        internal class Message : IHashable
        {
            private readonly string _value;

            public Message(string value) => _value = value;

            public string HashBy() => _value;

            public override string ToString() => _value;
        }

        internal class MyTestActor : IActor
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
    }
}