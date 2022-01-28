using System;
using System.Threading.Tasks;
using Proto.Router.Messages;
using Proto.TestFixtures;
using Proto.Tests;
using Xunit;

namespace Proto.Router.Tests
{
    public class BroadcastGroupTests : ActorTestBase
    {
        private static readonly Props MyActorProps = Props.FromProducer(() => new MyTestActor());
        private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(1000);


        [Fact]
        public async Task BroadcastGroupRouter_AllRouteesReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees(System);

            System.Root.Send(router, "hello");

            Assert.Equal("hello", await System.Root.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Equal("hello", await System.Root.RequestAsync<string>(routee2, "received?", _timeout));
            Assert.Equal("hello", await System.Root.RequestAsync<string>(routee3, "received?", _timeout));
        }

        [Fact]
        public async Task BroadcastGroupRouter_WhenOneRouteeIsStopped_AllOtherRouteesReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees(System);

            await System.Root.StopAsync(routee2);
            System.Root.Send(router, "hello");

            Assert.Equal("hello", await System.Root.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Equal("hello", await System.Root.RequestAsync<string>(routee3, "received?", _timeout));
        }

        [Fact]
        public async Task BroadcastGroupRouter_WhenOneRouteeIsSlow_AllOtherRouteesReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees(System);

            System.Root.Send(routee2, "go slow");
            System.Root.Send(router, "hello");

            Assert.Equal("hello", await System.Root.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Equal("hello", await System.Root.RequestAsync<string>(routee3, "received?", _timeout));
        }

        [Fact]
        public async Task BroadcastGroupRouter_RouteesCanBeRemoved()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees(System);

            System.Root.Send(router, new RouterRemoveRoutee(routee1));

            var routees = await System.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.DoesNotContain(routee1, routees.Pids);
            Assert.Contains(routee2, routees.Pids);
            Assert.Contains(routee3, routees.Pids);
        }

        [Fact]
        public async Task BroadcastGroupRouter_RouteesCanBeAdded()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees(System);
            var routee4 = System.Root.Spawn(MyActorProps);
            System.Root.Send(router, new RouterAddRoutee(routee4));

            var routees = await System.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.Contains(routee1, routees.Pids);
            Assert.Contains(routee2, routees.Pids);
            Assert.Contains(routee3, routees.Pids);
            Assert.Contains(routee4, routees.Pids);
        }

        [Fact]
        public async Task BroadcastGroupRouter_RemovedRouteesNoLongerReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees(System);

            System.Root.Send(router, "first message");
            System.Root.Send(router, new RouterRemoveRoutee(routee1));
            System.Root.Send(router, "second message");

            Assert.Equal("first message", await System.Root.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Equal("second message", await System.Root.RequestAsync<string>(routee2, "received?", _timeout));
            Assert.Equal("second message", await System.Root.RequestAsync<string>(routee3, "received?", _timeout));
        }

        [Fact]
        public async Task BroadcastGroupRouter_AddedRouteesReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees(System);
            var routee4 = System.Root.Spawn(MyActorProps);
            System.Root.Send(router, new RouterAddRoutee(routee4));
            System.Root.Send(router, "a message");

            Assert.Equal("a message", await System.Root.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Equal("a message", await System.Root.RequestAsync<string>(routee2, "received?", _timeout));
            Assert.Equal("a message", await System.Root.RequestAsync<string>(routee3, "received?", _timeout));
            Assert.Equal("a message", await System.Root.RequestAsync<string>(routee4, "received?", _timeout));
        }

        [Fact]
        public async Task BroadcastGroupRouter_AllRouteesReceiveRouterBroadcastMessages()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees(System);

            System.Root.Send(router, new RouterBroadcastMessage("hello"));

            Assert.Equal("hello", await System.Root.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Equal("hello", await System.Root.RequestAsync<string>(routee2, "received?", _timeout));
            Assert.Equal("hello", await System.Root.RequestAsync<string>(routee3, "received?", _timeout));
        }

        private static (PID router, PID routee1, PID routee2, PID routee3) CreateBroadcastGroupRouterWith3Routees(ActorSystem system)
        {
            var routee1 = system.Root.Spawn(MyActorProps);
            var routee2 = system.Root.Spawn(MyActorProps);
            var routee3 = system.Root.Spawn(MyActorProps);

            var props = system.Root.NewBroadcastGroup(routee1, routee2, routee3)
                .WithMailbox(() => new TestMailbox());
            var router = system.Root.Spawn(props);
            return (router, routee1, routee2, routee3);
        }

        private class MyTestActor : IActor
        {
            private string? _received;

            public async Task ReceiveAsync(IContext context)
            {
                switch (context.Message)
                {
                    case "received?":
                        context.Respond(_received!);
                        break;
                    case "go slow":
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