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
        private static readonly RootContext Context = new RootContext();
        private static readonly Props MyActorProps = Props.FromProducer(() => new MyTestActor())
            .WithMailbox(() => new TestMailbox());
        private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(1000);

        [Fact]
        public async void ConsistentHashGroupRouter_MessageWithSameHashAlwaysGoesToSameRoutee()
        {
            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees();

            Context.Send(router, new Message("message1"));
            Context.Send(router, new Message("message1"));
            Context.Send(router, new Message("message1"));

            Assert.Equal(3, await Context.RequestAsync<int>(routee1, "received?", _timeout));
            Assert.Equal(0, await Context.RequestAsync<int>(routee2, "received?", _timeout));
            Assert.Equal(0, await Context.RequestAsync<int>(routee3, "received?", _timeout));
        }

        [Fact]
        public async void ConsistentHashGroupRouter_MessagesWithDifferentHashesGoToDifferentRoutees()
        {
            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees();

            Context.Send(router, new Message("message1"));
            Context.Send(router, new Message("message2"));
            Context.Send(router, new Message("message3"));

            Assert.Equal(1, await Context.RequestAsync<int>(routee1, "received?", _timeout));
            Assert.Equal(1, await Context.RequestAsync<int>(routee2, "received?", _timeout));
            Assert.Equal(1, await Context.RequestAsync<int>(routee3, "received?", _timeout));
        }

        [Fact]
        public async void ConsistentHashGroupRouter_MessageWithSameHashAlwaysGoesToSameRoutee_EvenWhenNewRouteeAdded()
        {
            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees();

            Context.Send(router, new Message("message1"));
            var routee4 = Context.Spawn(MyActorProps);
            Context.Send(router, new RouterAddRoutee{PID = routee4});
            Context.Send(router, new Message("message1"));

            Assert.Equal(2, await Context.RequestAsync<int>(routee1, "received?", _timeout));
            Assert.Equal(0, await Context.RequestAsync<int>(routee2, "received?", _timeout));
            Assert.Equal(0, await Context.RequestAsync<int>(routee3, "received?", _timeout));
        }

        [Fact]
        public async void ConsistentHashGroupRouter_RouteesCanBeRemoved()
        {
            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees();

            Context.Send(router, new RouterRemoveRoutee { PID = routee1 });

            var routees = await Context.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.DoesNotContain(routee1, routees.PIDs);
            Assert.Contains(routee2, routees.PIDs);
            Assert.Contains(routee3, routees.PIDs);
        }

        [Fact]
        public async void ConsistentHashGroupRouter_RouteesCanBeAdded()
        {
            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees();
            var routee4 = Context.Spawn(MyActorProps);
            Context.Send(router, new RouterAddRoutee { PID = routee4 });

            var routees = await Context.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.Contains(routee1, routees.PIDs);
            Assert.Contains(routee2, routees.PIDs);
            Assert.Contains(routee3, routees.PIDs);
            Assert.Contains(routee4, routees.PIDs);
        }

        [Fact]
        public async void ConsistentHashGroupRouter_RemovedRouteesNoLongerReceiveMessages()
        {
            var (router, routee1, _, _) = CreateRouterWith3Routees();
            
            Context.Send(router, new RouterRemoveRoutee { PID = routee1 });
            Context.Send(router, new Message("message1"));
            Assert.Equal(0, await Context.RequestAsync<int>(routee1, "received?", _timeout));
        }

        [Fact]
        public async void ConsistentHashGroupRouter_AddedRouteesReceiveMessages()
        {
            var (router, _, _, _) = CreateRouterWith3Routees();
            var routee4 = Context.Spawn(MyActorProps);
            Context.Send(router, new RouterAddRoutee { PID = routee4 });
            Context.Send(router, new Message("message4"));
            Assert.Equal(1, await Context.RequestAsync<int>(routee4, "received?", _timeout));
        }

        [Fact]
        public async void ConsistentHashGroupRouter_MessageIsReassignedWhenRouteeRemoved()
        {
            var (router, routee1, routee2, _) = CreateRouterWith3Routees();

            Context.Send(router, new Message("message1"));
            // routee1 handles "message1"
            Assert.Equal(1, await Context.RequestAsync<int>(routee1, "received?", _timeout));
            // remove receiver
            Context.Send(router, new RouterRemoveRoutee { PID = routee1 });
            // routee2 should now handle "message1"
            Context.Send(router, new Message("message1"));

            Assert.Equal(1, await Context.RequestAsync<int>(routee2, "received?", _timeout));
        }

        [Fact]
        public async void ConsistentHashGroupRouter_AllRouteesReceiveRouterBroadcastMessages()
        {
            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees();

            Context.Send(router, new RouterBroadcastMessage { Message = new Message("hello") });

            Assert.Equal(1, await Context.RequestAsync<int>(routee1, "received?", _timeout));
            Assert.Equal(1, await Context.RequestAsync<int>(routee2, "received?", _timeout));
            Assert.Equal(1, await Context.RequestAsync<int>(routee3, "received?", _timeout));
        }

        private (PID router, PID routee1, PID routee2, PID routee3) CreateRouterWith3Routees()
        {
            // assign unique names for when tests run in parallel
            var routee1 = Context.SpawnNamed(MyActorProps, Guid.NewGuid() + "routee1");
            var routee2 = Context.SpawnNamed(MyActorProps, Guid.NewGuid() + "routee2");
            var routee3 = Context.SpawnNamed(MyActorProps, Guid.NewGuid() + "routee3");

            var props = Router.NewConsistentHashGroup(SuperIntelligentDeterministicHash.Hash, 1, routee1, routee2, routee3)
                .WithMailbox(() => new TestMailbox());
            var router = Context.Spawn(props);
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

            public Message(string value)
            {
                _value = value;
            }
            public string HashBy()
            {
                return _value;
            }

            public override string ToString()
            {
                return _value;
            }
        }

        internal class MyTestActor : IActor
        {
            private readonly List<string> _receivedMessages = new List<string>();
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
                }
                return Actor.Done;
            }
        }
    }
}
