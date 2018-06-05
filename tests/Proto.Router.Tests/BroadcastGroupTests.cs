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
        public async void BroadcastGroupRouter_AllRouteesReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees();

            RootContext.Empty.Send(router, "hello");

            Assert.Equal("hello", await RootContext.Empty.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Equal("hello", await RootContext.Empty.RequestAsync<string>(routee2, "received?", _timeout));
            Assert.Equal("hello", await RootContext.Empty.RequestAsync<string>(routee3, "received?", _timeout));
        }

        [Fact]
        public async void BroadcastGroupRouter_WhenOneRouteeIsStopped_AllOtherRouteesReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees();

            await routee2.StopAsync();
            RootContext.Empty.Send(router, "hello");

            Assert.Equal("hello", await RootContext.Empty.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Equal("hello", await RootContext.Empty.RequestAsync<string>(routee3, "received?", _timeout));
        }

        [Fact]
        public async void BroadcastGroupRouter_WhenOneRouteeIsSlow_AllOtherRouteesReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees();

            RootContext.Empty.Send(routee2, "go slow");
            RootContext.Empty.Send(router, "hello");

            Assert.Equal("hello", await RootContext.Empty.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Equal("hello", await RootContext.Empty.RequestAsync<string>(routee3, "received?", _timeout));
        }

        [Fact]
        public async void BroadcastGroupRouter_RouteesCanBeRemoved()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees();

            RootContext.Empty.Send(router, new RouterRemoveRoutee { PID = routee1 });

            var routees = await RootContext.Empty.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.DoesNotContain(routee1, routees.PIDs);
            Assert.Contains(routee2, routees.PIDs);
            Assert.Contains(routee3, routees.PIDs);
        }

        [Fact]
        public async void BroadcastGroupRouter_RouteesCanBeAdded()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees();
            var routee4 = Actor.Spawn(MyActorProps);
            RootContext.Empty.Send(router, new RouterAddRoutee { PID = routee4 });

            var routees = await RootContext.Empty.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.Contains(routee1, routees.PIDs);
            Assert.Contains(routee2, routees.PIDs);
            Assert.Contains(routee3, routees.PIDs);
            Assert.Contains(routee4, routees.PIDs);
        }

        [Fact]
        public async void BroadcastGroupRouter_RemovedRouteesNoLongerReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees();

            RootContext.Empty.Send(router, "first message");
            RootContext.Empty.Send(router, new RouterRemoveRoutee { PID = routee1 });
            RootContext.Empty.Send(router, "second message");

            Assert.Equal("first message", await RootContext.Empty.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Equal("second message", await RootContext.Empty.RequestAsync<string>(routee2, "received?", _timeout));
            Assert.Equal("second message", await RootContext.Empty.RequestAsync<string>(routee3, "received?", _timeout));
        }

        [Fact]
        public async void BroadcastGroupRouter_AddedRouteesReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees();
            var routee4 = Actor.Spawn(MyActorProps);
            RootContext.Empty.Send(router, new RouterAddRoutee { PID = routee4 });
            RootContext.Empty.Send(router, "a message");

            Assert.Equal("a message", await RootContext.Empty.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Equal("a message", await RootContext.Empty.RequestAsync<string>(routee2, "received?", _timeout));
            Assert.Equal("a message", await RootContext.Empty.RequestAsync<string>(routee3, "received?", _timeout));
            Assert.Equal("a message", await RootContext.Empty.RequestAsync<string>(routee4, "received?", _timeout));
        }

        [Fact]
        public async void BroadcastGroupRouter_AllRouteesReceiveRouterBroadcastMessages()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees();

            RootContext.Empty.Send(router, new RouterBroadcastMessage { Message = "hello" });

            Assert.Equal("hello", await RootContext.Empty.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Equal("hello", await RootContext.Empty.RequestAsync<string>(routee2, "received?", _timeout));
            Assert.Equal("hello", await RootContext.Empty.RequestAsync<string>(routee3, "received?", _timeout));
        }

        private (PID router, PID routee1, PID routee2, PID routee3) CreateBroadcastGroupRouterWith3Routees()
        {
            var routee1 = Actor.Spawn(MyActorProps);
            var routee2 = Actor.Spawn(MyActorProps);
            var routee3 = Actor.Spawn(MyActorProps);

            var props = Router.NewBroadcastGroup(routee1, routee2, routee3)
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
                        context.Respond(_received);
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
