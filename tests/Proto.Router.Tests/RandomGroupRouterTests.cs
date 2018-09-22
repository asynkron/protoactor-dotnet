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
        private static readonly RootContext Context = new RootContext();
        private static readonly Props MyActorProps = Props.FromProducer(() => new MyTestActor());
        private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(1000);

        [Fact]
        public async void RandomGroupRouter_RouteesReceiveMessagesInRandomOrder()
        {
            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees();

            Context.Send(router, "1");
            Context.Send(router, "2");
            Context.Send(router, "3");

            Assert.Equal("2", await Context.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Equal("3", await Context.RequestAsync<string>(routee2, "received?", _timeout));
            Assert.Equal("1", await Context.RequestAsync<string>(routee3, "received?", _timeout));
        }

        [Fact]
        public async void RandomGroupRouter_NewlyAddedRouteesReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees();
            var routee4 = Context.Spawn(MyActorProps);
            Context.Send(router, new RouterAddRoutee
            {
                PID = routee4
            });
            Context.Send(router, "1");
            Context.Send(router, "2");
            Context.Send(router, "3");
            Context.Send(router, "4");

            // results are random! (but consistent due to seeding) As MyTestActor only stores the most
            // recent message, "1" is overwritten by a subsequent message. 
            Assert.Equal("2", await Context.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Null(await Context.RequestAsync<string>(routee2, "received?", _timeout));
            Assert.Equal("3", await Context.RequestAsync<string>(routee3, "received?", _timeout));
            Assert.Equal("4", await Context.RequestAsync<string>(routee4, "received?", _timeout));
        }

        [Fact]
        public async void RandomGroupRouter_RemovedRouteesDoNotReceiveMessages()
        {
            var (router, routee1, _, _) = CreateRouterWith3Routees();
            Context.Send(router, new RouterRemoveRoutee
            {
                PID = routee1
            });
            for (int i = 0; i < 100; i++)
            {
                Context.Send(router, i.ToString());
            }
            Assert.Null(await Context.RequestAsync<string>(routee1, "received?", _timeout));
        }

        [Fact]
        public async void RandomGroupRouter_RouteesCanBeRemoved()
        {
            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees();

            Context.Send(router, new RouterRemoveRoutee { PID = routee1 });

            var routees = await Context.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.DoesNotContain(routee1, routees.PIDs);
            Assert.Contains(routee2, routees.PIDs);
            Assert.Contains(routee3, routees.PIDs);
        }

        [Fact]
        public async void RandomGroupRouter_RouteesCanBeAdded()
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
        public async void RandomGroupRouter_AllRouteesReceiveRouterBroadcastMessages()
        {
            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees();

            Context.Send(router, new RouterBroadcastMessage { Message = "hello" });

            Assert.Equal("hello", await Context.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Equal("hello", await Context.RequestAsync<string>(routee2, "received?", _timeout));
            Assert.Equal("hello", await Context.RequestAsync<string>(routee3, "received?", _timeout));
        }

        private (PID router, PID routee1, PID routee2, PID routee3) CreateRouterWith3Routees()
        {
            var routee1 = Context.Spawn(MyActorProps);
            var routee2 = Context.Spawn(MyActorProps);
            var routee3 = Context.Spawn(MyActorProps);

            var props = Router.NewRandomGroup(10000, routee1, routee2, routee3)
                .WithMailbox(() => new TestMailbox());
            var router = Context.Spawn(props);
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
