using System;
using System.Threading.Tasks;
using Proto.Router.Messages;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Router.Tests
{
    public class RoundRobinGroupTests
    {
        private static readonly RootContext Context = new RootContext();
        private static readonly Props MyActorProps = Props.FromProducer(() => new MyTestActor())
                                                          .WithMailbox(() => new TestMailbox());
        private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(1000);

        [Fact]
        public async void RoundRobinGroupRouter_RouteesReceiveMessagesInRoundRobinStyle()
        {
            var (router, routee1, routee2, routee3) = CreateRoundRobinRouterWith3Routees();

            Context.Send(router, "1");

            // only routee1 has received the message
            Assert.Equal("1", await Context.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Equal(null, await Context.RequestAsync<string>(routee2, "received?", _timeout));
            Assert.Equal(null, await Context.RequestAsync<string>(routee3, "received?", _timeout));

            Context.Send(router, "2");
            Context.Send(router, "3");

            // routees 2 and 3 receive next messages
            Assert.Equal("1", await Context.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Equal("2", await Context.RequestAsync<string>(routee2, "received?", _timeout));
            Assert.Equal("3", await Context.RequestAsync<string>(routee3, "received?", _timeout));

            Context.Send(router, "4");

            // Round robin kicks in and routee1 recevies next message
            Assert.Equal("4", await Context.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Equal("2", await Context.RequestAsync<string>(routee2, "received?", _timeout));
            Assert.Equal("3", await Context.RequestAsync<string>(routee3, "received?", _timeout));
        }

        [Fact]
        public async void RoundRobinGroupRouter_RouteesCanBeRemoved()
        {
            var (router, routee1, routee2, routee3) = CreateRoundRobinRouterWith3Routees();

            Context.Send(router, new RouterRemoveRoutee { PID = routee1 });

            var routees = await Context.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.DoesNotContain(routee1, routees.PIDs);
            Assert.Contains(routee2, routees.PIDs);
            Assert.Contains(routee3, routees.PIDs);
        }

        [Fact]
        public async void RoundRobinGroupRouter_RouteesCanBeAdded()
        {
            var (router, routee1, routee2, routee3) = CreateRoundRobinRouterWith3Routees();
            var routee4 = Context.Spawn(MyActorProps);
            Context.Send(router, new RouterAddRoutee { PID = routee4 });

            var routees = await Context.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.Contains(routee1, routees.PIDs);
            Assert.Contains(routee2, routees.PIDs);
            Assert.Contains(routee3, routees.PIDs);
            Assert.Contains(routee4, routees.PIDs);
        }

        [Fact]
        public async void RoundRobinGroupRouter_RemovedRouteesNoLongerReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateRoundRobinRouterWith3Routees();

            Context.Send(router, "0");
            Context.Send(router, "0");
            Context.Send(router, "0");
            Context.Send(router, new RouterRemoveRoutee { PID = routee1 });
            // we should have 2 routees, so send 3 messages to ensure round robin happens
            Context.Send(router, "3");
            Context.Send(router, "3");
            Context.Send(router, "3");

            Assert.Equal("0", await Context.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Equal("3", await Context.RequestAsync<string>(routee2, "received?", _timeout));
            Assert.Equal("3", await Context.RequestAsync<string>(routee3, "received?", _timeout));
        }

        [Fact]
        public async void RoundRobinGroupRouter_AddedRouteesReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateRoundRobinRouterWith3Routees();
            var routee4 = Context.Spawn(MyActorProps);
            Context.Send(router, new RouterAddRoutee { PID = routee4 });
            // should now have 4 routees, so need to send 4 messages to ensure all get them
            Context.Send(router, "1");
            Context.Send(router, "1");
            Context.Send(router, "1");
            Context.Send(router, "1");

            Assert.Equal("1", await Context.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Equal("1", await Context.RequestAsync<string>(routee2, "received?", _timeout));
            Assert.Equal("1", await Context.RequestAsync<string>(routee3, "received?", _timeout));
            Assert.Equal("1", await Context.RequestAsync<string>(routee4, "received?", _timeout));
        }

        [Fact]
        public async void RoundRobinGroupRouter_AllRouteesReceiveRouterBroadcastMessages()
        {
            var (router, routee1, routee2, routee3) = CreateRoundRobinRouterWith3Routees();
            
            Context.Send(router, new RouterBroadcastMessage { Message = "hello" });

            Assert.Equal("hello", await Context.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Equal("hello", await Context.RequestAsync<string>(routee2, "received?", _timeout));
            Assert.Equal("hello", await Context.RequestAsync<string>(routee3, "received?", _timeout));
        }
        
        private (PID router, PID routee1, PID routee2, PID routee3) CreateRoundRobinRouterWith3Routees()
        {
            var routee1 = Context.Spawn(MyActorProps);
            var routee2 = Context.Spawn(MyActorProps);
            var routee3 = Context.Spawn(MyActorProps);

            var props = Router.NewRoundRobinGroup(routee1, routee2, routee3)
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