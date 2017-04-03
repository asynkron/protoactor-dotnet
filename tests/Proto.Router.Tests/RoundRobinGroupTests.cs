using System;
using System.Threading.Tasks;
using Proto.Router.Messages;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Router.Tests
{
    public class RoundRobinGroupTests
    {
        private static readonly Props MyActorProps = Actor.FromProducer(() => new MyTestActor())
                                                          .WithMailbox(() => new TestMailbox());
        private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(250);

        [Fact]
        public async void RoundRobinGroupRouter_RouteesReceiveMessagesInRoundRobinStyle()
        {
            var (router, routee1, routee2, routee3) = CreateRoundRobinRouterWith3Routees();

            router.Tell("1");

            // only routee1 has received the message
            Assert.Equal("1", await routee1.RequestAsync<string>("received?", _timeout));
            Assert.Equal(null, await routee2.RequestAsync<string>("received?", _timeout));
            Assert.Equal(null, await routee3.RequestAsync<string>("received?", _timeout));

            router.Tell("2");
            router.Tell("3");

            // routees 2 and 3 receive next messages
            Assert.Equal("1", await routee1.RequestAsync<string>("received?", _timeout));
            Assert.Equal("2", await routee2.RequestAsync<string>("received?", _timeout));
            Assert.Equal("3", await routee3.RequestAsync<string>("received?", _timeout));

            router.Tell("4");

            // Round robin kicks in and routee1 recevies next message
            Assert.Equal("4", await routee1.RequestAsync<string>("received?", _timeout));
            Assert.Equal("2", await routee2.RequestAsync<string>("received?", _timeout));
            Assert.Equal("3", await routee3.RequestAsync<string>("received?", _timeout));
        }

        [Fact]
        public async void RoundRobinGroupRouter_RouteesCanBeRemoved()
        {
            var (router, routee1, routee2, routee3) = CreateRoundRobinRouterWith3Routees();

            router.Tell(new RouterRemoveRoutee { PID = routee1 });

            var routees = await router.RequestAsync<Routees>(new RouterGetRoutees());
            Assert.DoesNotContain(routee1, routees.PIDs);
            Assert.Contains(routee2, routees.PIDs);
            Assert.Contains(routee3, routees.PIDs);
        }

        [Fact]
        public async void RoundRobinGroupRouter_RouteesCanBeAdded()
        {
            var (router, routee1, routee2, routee3) = CreateRoundRobinRouterWith3Routees();
            var routee4 = Actor.Spawn(MyActorProps);
            router.Tell(new RouterAddRoutee { PID = routee4 });

            var routees = await router.RequestAsync<Routees>(new RouterGetRoutees());
            Assert.Contains(routee1, routees.PIDs);
            Assert.Contains(routee2, routees.PIDs);
            Assert.Contains(routee3, routees.PIDs);
            Assert.Contains(routee4, routees.PIDs);
        }

        [Fact]
        public async void RoundRobinGroupRouter_RemovedRouteesNoLongerReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateRoundRobinRouterWith3Routees();

            router.Tell("0");
            router.Tell("0");
            router.Tell("0");
            router.Tell(new RouterRemoveRoutee { PID = routee1 });
            // we should have 2 routees, so send 3 messages to ensure round robin happens
            router.Tell("3");
            router.Tell("3");
            router.Tell("3");

            Assert.Equal("0", await routee1.RequestAsync<string>("received?", _timeout));
            Assert.Equal("3", await routee2.RequestAsync<string>("received?", _timeout));
            Assert.Equal("3", await routee3.RequestAsync<string>("received?", _timeout));
        }

        [Fact]
        public async void RoundRobinGroupRouter_AddedRouteesReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateRoundRobinRouterWith3Routees();
            var routee4 = Actor.Spawn(MyActorProps);
            router.Tell(new RouterAddRoutee { PID = routee4 });
            // should now have 4 routees, so need to send 4 messages to ensure all get them
            router.Tell("1");
            router.Tell("1");
            router.Tell("1");
            router.Tell("1");

            Assert.Equal("1", await routee1.RequestAsync<string>("received?", _timeout));
            Assert.Equal("1", await routee2.RequestAsync<string>("received?", _timeout));
            Assert.Equal("1", await routee3.RequestAsync<string>("received?", _timeout));
            Assert.Equal("1", await routee4.RequestAsync<string>("received?", _timeout));
        }

        [Fact]
        public async void RoundRobinGroupRouter_AllRouteesReceiveRouterBroadcastMessages()
        {
            var (router, routee1, routee2, routee3) = CreateRoundRobinRouterWith3Routees();
            
            router.Tell(new RouterBroadcastMessage { Message = "hello" });

            Assert.Equal("hello", await routee1.RequestAsync<string>("received?", _timeout));
            Assert.Equal("hello", await routee2.RequestAsync<string>("received?", _timeout));
            Assert.Equal("hello", await routee3.RequestAsync<string>("received?", _timeout));
        }
        
        private (PID router, PID routee1, PID routee2, PID routee3) CreateRoundRobinRouterWith3Routees()
        {
            var routee1 = Actor.Spawn(MyActorProps);
            var routee2 = Actor.Spawn(MyActorProps);
            var routee3 = Actor.Spawn(MyActorProps);

            var props = Router.NewRoundRobinGroup(MyActorProps, routee1, routee2, routee3)
                .WithMailbox(() => new TestMailbox());
            var router = Actor.Spawn(props);
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
                        context.Sender.Tell(_received);
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