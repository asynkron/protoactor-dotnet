using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Proto.Router.Messages;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Router.Tests
{
    public class RandomGroupRouterTests
    {
        private readonly ActorSystem ActorSystem = new ActorSystem();
        private static readonly Props MyActorProps = Props.FromProducer(() => new MyTestActor());
        private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(1000);

        [Fact]
        public async Task RandomGroupRouter_RouteesReceiveMessagesInRandomOrder()
        {
            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees(ActorSystem);

            ActorSystem.Root.Send(router, "1");
            ActorSystem.Root.Send(router, "2");
            ActorSystem.Root.Send(router, "3");

            Assert.Equal("2", await ActorSystem.Root.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Equal("3", await ActorSystem.Root.RequestAsync<string>(routee2, "received?", _timeout));
            Assert.Equal("1", await ActorSystem.Root.RequestAsync<string>(routee3, "received?", _timeout));
        }

        [Fact]
        public async Task RandomGroupRouter_NewlyAddedRouteesReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees(ActorSystem);
            var routee4 = ActorSystem.Root.Spawn(MyActorProps);
            ActorSystem.Root.Send(router, new RouterAddRoutee(routee4));
            ActorSystem.Root.Send(router, "1");
            ActorSystem.Root.Send(router, "2");
            ActorSystem.Root.Send(router, "3");
            ActorSystem.Root.Send(router, "4");

            // results are random! (but consistent due to seeding) As MyTestActor only stores the most
            // recent message, "1" is overwritten by a subsequent message. 
            Assert.Equal("2", await ActorSystem.Root.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Null(await ActorSystem.Root.RequestAsync<string>(routee2, "received?", _timeout));
            Assert.Equal("3", await ActorSystem.Root.RequestAsync<string>(routee3, "received?", _timeout));
            Assert.Equal("4", await ActorSystem.Root.RequestAsync<string>(routee4, "received?", _timeout));
        }

        [Fact]
        public async Task RandomGroupRouter_RemovedRouteesDoNotReceiveMessages()
        {
            var (router, routee1, _, _) = CreateRouterWith3Routees(ActorSystem);

            ActorSystem.Root.Send(router, new RouterRemoveRoutee(routee1));

            for (int i = 0; i < 100; i++)
            {
                ActorSystem.Root.Send(router, i.ToString());
            }

            Assert.Null(await ActorSystem.Root.RequestAsync<string>(routee1, "received?", _timeout));
        }

        [Fact]
        public async Task RandomGroupRouter_RouteesCanBeRemoved()
        {
            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees(ActorSystem);

            ActorSystem.Root.Send(router, new RouterRemoveRoutee(routee1));

            var routees = await ActorSystem.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.DoesNotContain(routee1, routees.PIDs);
            Assert.Contains(routee2, routees.PIDs);
            Assert.Contains(routee3, routees.PIDs);
        }

        [Fact]
        public async Task RandomGroupRouter_RouteesCanBeAdded()
        {
            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees(ActorSystem);
            var routee4 = ActorSystem.Root.Spawn(MyActorProps);
            ActorSystem.Root.Send(router, new RouterAddRoutee(routee4));

            var routees = await ActorSystem.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.Contains(routee1, routees.PIDs);
            Assert.Contains(routee2, routees.PIDs);
            Assert.Contains(routee3, routees.PIDs);
            Assert.Contains(routee4, routees.PIDs);
        }

        [Fact]
        public async Task RandomGroupRouter_AllRouteesReceiveRouterBroadcastMessages()
        {
            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees(ActorSystem);

            ActorSystem.Root.Send(router, new RouterBroadcastMessage("hello"));

            Assert.Equal("hello", await ActorSystem.Root.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Equal("hello", await ActorSystem.Root.RequestAsync<string>(routee2, "received?", _timeout));
            Assert.Equal("hello", await ActorSystem.Root.RequestAsync<string>(routee3, "received?", _timeout));
        }

        private (PID router, PID routee1, PID routee2, PID routee3) CreateRouterWith3Routees(ActorSystem system)
        {
            var routee1 = system.Root.Spawn(MyActorProps);
            var routee2 = system.Root.Spawn(MyActorProps);
            var routee3 = system.Root.Spawn(MyActorProps);

            var props = system.Root.NewRandomGroup(10000, routee1, routee2, routee3)
                .WithMailbox(() => new TestMailbox());
            var router = system.Root.Spawn(props);
            return (router, routee1, routee2, routee3);
        }

        internal class MyTestActor : IActor
        {
            private string _received;

            public Task ReceiveAsync(IContext context)
            {
                switch (context.Message)
                {
                    case string msg when msg == "received?":
                        context.Respond(_received);
                        break;
                    case string msg:
                        _received = msg;
                        break;
                }

                return Actor.Done;
            }
        }
    }
}
