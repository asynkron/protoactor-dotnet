using System;
using System.Threading.Tasks;
using Xunit;
using Proto.Router.Messages;
using Proto.TestFixtures;

namespace Proto.Router.Tests
{
    public class BroadcastGroupTests
    {
        private static readonly Props MyActorProps = Actor.FromProducer(() => new MyTestActor());
        private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(250);

        [Fact]
        public async void BroadcastGroupRouter_AllRouteesReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees();

            router.Tell("hello");

            Assert.Equal("hello", await routee1.RequestAsync<string>("received?", _timeout));
            Assert.Equal("hello", await routee2.RequestAsync<string>("received?", _timeout));
            Assert.Equal("hello", await routee3.RequestAsync<string>("received?", _timeout));
        }

        [Fact]
        public async void BroadcastGroupRouter_WhenOneRouteeIsStopped_AllOtherRouteesReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees();

            routee2.Stop();
            router.Tell("hello");

            Assert.Equal("hello", await routee1.RequestAsync<string>("received?", _timeout));
            Assert.Equal("hello", await routee3.RequestAsync<string>("received?", _timeout));
        }

        [Fact]
        public async void BroadcastGroupRouter_WhenOneRouteeIsSlow_AllOtherRouteesReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees();

            routee2.Tell("go slow");
            router.Tell("hello");

            Assert.Equal("hello", await routee1.RequestAsync<string>("received?", _timeout));
            Assert.Equal("hello", await routee3.RequestAsync<string>("received?", _timeout));
        }

        [Fact]
        public async void BroadcastGroupRouter_RouteesCanBeRemoved()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees();

            router.Tell(new RouterRemoveRoutee { PID = routee1 });

            var routees = await router.RequestAsync<Routees>(new RouterGetRoutees());
            Assert.DoesNotContain(routee1, routees.PIDs);
            Assert.Contains(routee2, routees.PIDs);
            Assert.Contains(routee3, routees.PIDs);
        }

        [Fact]
        public async void BroadcastGroupRouter_RouteesCanBeAdded()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees();
            var routee4 = Actor.Spawn(MyActorProps);
            router.Tell(new RouterAddRoutee { PID = routee4 });

            var routees = await router.RequestAsync<Routees>(new RouterGetRoutees());
            Assert.Contains(routee1, routees.PIDs);
            Assert.Contains(routee2, routees.PIDs);
            Assert.Contains(routee3, routees.PIDs);
            Assert.Contains(routee4, routees.PIDs);
        }

        [Fact]
        public async void BroadcastGroupRouter_RemovedRouteesNoLongerReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees();

            router.Tell("first message");
            router.Tell(new RouterRemoveRoutee { PID = routee1 });
            router.Tell("second message");

            Assert.Equal("first message", await routee1.RequestAsync<string>("received?", _timeout));
            Assert.Equal("second message", await routee2.RequestAsync<string>("received?", _timeout));
            Assert.Equal("second message", await routee3.RequestAsync<string>("received?", _timeout));
        }

        [Fact]
        public async void BroadcastGroupRouter_AddedRouteesReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees();
            var routee4 = Actor.Spawn(MyActorProps);
            router.Tell(new RouterAddRoutee { PID = routee4 });
            router.Tell("a message");

            Assert.Equal("a message", await routee1.RequestAsync<string>("received?", _timeout));
            Assert.Equal("a message", await routee2.RequestAsync<string>("received?", _timeout));
            Assert.Equal("a message", await routee3.RequestAsync<string>("received?", _timeout));
            Assert.Equal("a message", await routee4.RequestAsync<string>("received?", _timeout));
        }

        [Fact]
        public async void BroadcastGroupRouter_AllRouteesReceiveRouterBroadcastMessages()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees();

            router.Tell(new RouterBroadcastMessage { Message = "hello" });

            Assert.Equal("hello", await routee1.RequestAsync<string>("received?", _timeout));
            Assert.Equal("hello", await routee2.RequestAsync<string>("received?", _timeout));
            Assert.Equal("hello", await routee3.RequestAsync<string>("received?", _timeout));
        }

        private (PID router, PID routee1, PID routee2, PID routee3) CreateBroadcastGroupRouterWith3Routees()
        {
            var routee1 = Actor.Spawn(MyActorProps);
            var routee2 = Actor.Spawn(MyActorProps);
            var routee3 = Actor.Spawn(MyActorProps);

            var props = Router.NewBroadcastGroup(MyActorProps, routee1, routee2, routee3)
                .WithMailbox(() => new TestMailbox());
            var router = Actor.Spawn(props);
            return (router, routee1, routee2, routee3);
        }

        internal class MyTestActor : IActor
        {
            private string _received;
            public async Task ReceiveAsync(IContext context)
            {
                switch (context.Message)
                {
                    case string msg when msg == "received?":
                        context.Sender.Tell(_received);
                        break;
                    case string msg when msg == "go slow":
                        await Task.Delay(5000);
                        break;
                    case string msg:
                        _received = msg;
                        break;
                }
            }
        }
    }
}
