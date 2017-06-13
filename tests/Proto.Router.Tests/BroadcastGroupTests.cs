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
        private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(1000);

        [Fact]
        public async Task BroadcastGroupRouter_AllRouteesReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees();

            await router.SendAsync("hello");

            Assert.Equal("hello", await routee1.RequestAsync<string>("received?", _timeout));
            Assert.Equal("hello", await routee2.RequestAsync<string>("received?", _timeout));
            Assert.Equal("hello", await routee3.RequestAsync<string>("received?", _timeout));
        }

        [Fact]
        public async Task BroadcastGroupRouter_WhenOneRouteeIsStopped_AllOtherRouteesReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees();

            await routee2.StopAsync();
            await router.SendAsync("hello");

            Assert.Equal("hello", await routee1.RequestAsync<string>("received?", _timeout));
            Assert.Equal("hello", await routee3.RequestAsync<string>("received?", _timeout));
        }

        [Fact]
        public async Task BroadcastGroupRouter_WhenOneRouteeIsSlow_AllOtherRouteesReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees();

            await routee2.SendAsync("go slow");
            await router.SendAsync("hello");

            Assert.Equal("hello", await routee1.RequestAsync<string>("received?", _timeout));
            Assert.Equal("hello", await routee3.RequestAsync<string>("received?", _timeout));
        }

        [Fact]
        public async Task BroadcastGroupRouter_RouteesCanBeRemoved()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees();

            await router.SendAsync(new RouterRemoveRoutee { PID = routee1 });

            var routees = await router.RequestAsync<Routees>(new RouterGetRoutees(), _timeout);
            Assert.DoesNotContain(routee1, routees.PIDs);
            Assert.Contains(routee2, routees.PIDs);
            Assert.Contains(routee3, routees.PIDs);
        }

        [Fact]
        public async Task BroadcastGroupRouter_RouteesCanBeAdded()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees();
            var routee4 = Actor.Spawn(MyActorProps);
            await router.SendAsync(new RouterAddRoutee { PID = routee4 });

            var routees = await router.RequestAsync<Routees>(new RouterGetRoutees(), _timeout);
            Assert.Contains(routee1, routees.PIDs);
            Assert.Contains(routee2, routees.PIDs);
            Assert.Contains(routee3, routees.PIDs);
            Assert.Contains(routee4, routees.PIDs);
        }

        [Fact]
        public async Task BroadcastGroupRouter_RemovedRouteesNoLongerReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees();

            await router.SendAsync("first message");
            await router.SendAsync(new RouterRemoveRoutee { PID = routee1 });
            await router.SendAsync("second message");

            Assert.Equal("first message", await routee1.RequestAsync<string>("received?", _timeout));
            Assert.Equal("second message", await routee2.RequestAsync<string>("received?", _timeout));
            Assert.Equal("second message", await routee3.RequestAsync<string>("received?", _timeout));
        }

        [Fact]
        public async Task BroadcastGroupRouter_AddedRouteesReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees();
            var routee4 = Actor.Spawn(MyActorProps);
            await router.SendAsync(new RouterAddRoutee { PID = routee4 });
            await router.SendAsync("a message");

            Assert.Equal("a message", await routee1.RequestAsync<string>("received?", _timeout));
            Assert.Equal("a message", await routee2.RequestAsync<string>("received?", _timeout));
            Assert.Equal("a message", await routee3.RequestAsync<string>("received?", _timeout));
            Assert.Equal("a message", await routee4.RequestAsync<string>("received?", _timeout));
        }

        [Fact]
        public async Task BroadcastGroupRouter_AllRouteesReceiveRouterBroadcastMessages()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees();

            await router.SendAsync(new RouterBroadcastMessage { Message = "hello" });

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
                        await context.Sender.SendAsync(_received);
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
