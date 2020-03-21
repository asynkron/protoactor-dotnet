using System;
using System.Threading.Tasks;
using Xunit;
using Proto.Router.Messages;
using Proto.TestFixtures;

namespace Proto.Router.Tests
{
    public class BroadcastGroupTests
    {
        private readonly ActorSystem ActorSystem = new ActorSystem();
        private static readonly Props MyActorProps = Props.FromProducer(() => new MyTestActor());
        private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(1000);

        [Fact]
        public async Task BroadcastGroupRouter_AllRouteesReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees(ActorSystem);

            ActorSystem.Root.Send(router, "hello");

            Assert.Equal("hello", await ActorSystem.Root.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Equal("hello", await ActorSystem.Root.RequestAsync<string>(routee2, "received?", _timeout));
            Assert.Equal("hello", await ActorSystem.Root.RequestAsync<string>(routee3, "received?", _timeout));
        }

        [Fact]
        public async Task BroadcastGroupRouter_WhenOneRouteeIsStopped_AllOtherRouteesReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees(ActorSystem);

            await ActorSystem.Root.StopAsync(routee2);
            ActorSystem.Root.Send(router, "hello");

            Assert.Equal("hello", await ActorSystem.Root.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Equal("hello", await ActorSystem.Root.RequestAsync<string>(routee3, "received?", _timeout));
        }

        [Fact]
        public async Task BroadcastGroupRouter_WhenOneRouteeIsSlow_AllOtherRouteesReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees(ActorSystem);

            ActorSystem.Root.Send(routee2, "go slow");
            ActorSystem.Root.Send(router, "hello");

            Assert.Equal("hello", await ActorSystem.Root.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Equal("hello", await ActorSystem.Root.RequestAsync<string>(routee3, "received?", _timeout));
        }

        [Fact]
        public async Task BroadcastGroupRouter_RouteesCanBeRemoved()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees(ActorSystem);

            ActorSystem.Root.Send(router, new RouterRemoveRoutee(routee1));

            var routees = await ActorSystem.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.DoesNotContain(routee1, routees.PIDs);
            Assert.Contains(routee2, routees.PIDs);
            Assert.Contains(routee3, routees.PIDs);
        }

        [Fact]
        public async Task BroadcastGroupRouter_RouteesCanBeAdded()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees(ActorSystem);
            var routee4 = ActorSystem.Root.Spawn(MyActorProps);
            ActorSystem.Root.Send(router, new RouterAddRoutee(routee4));

            var routees = await ActorSystem.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.Contains(routee1, routees.PIDs);
            Assert.Contains(routee2, routees.PIDs);
            Assert.Contains(routee3, routees.PIDs);
            Assert.Contains(routee4, routees.PIDs);
        }

        [Fact]
        public async Task BroadcastGroupRouter_RemovedRouteesNoLongerReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees(ActorSystem);

            ActorSystem.Root.Send(router, "first message");
            ActorSystem.Root.Send(router, new RouterRemoveRoutee(routee1));
            ActorSystem.Root.Send(router, "second message");

            Assert.Equal("first message", await ActorSystem.Root.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Equal("second message", await ActorSystem.Root.RequestAsync<string>(routee2, "received?", _timeout));
            Assert.Equal("second message", await ActorSystem.Root.RequestAsync<string>(routee3, "received?", _timeout));
        }

        [Fact]
        public async Task BroadcastGroupRouter_AddedRouteesReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees(ActorSystem);
            var routee4 = ActorSystem.Root.Spawn(MyActorProps);
            ActorSystem.Root.Send(router, new RouterAddRoutee(routee4));
            ActorSystem.Root.Send(router, "a message");

            Assert.Equal("a message", await ActorSystem.Root.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Equal("a message", await ActorSystem.Root.RequestAsync<string>(routee2, "received?", _timeout));
            Assert.Equal("a message", await ActorSystem.Root.RequestAsync<string>(routee3, "received?", _timeout));
            Assert.Equal("a message", await ActorSystem.Root.RequestAsync<string>(routee4, "received?", _timeout));
        }

        [Fact]
        public async Task BroadcastGroupRouter_AllRouteesReceiveRouterBroadcastMessages()
        {
            var (router, routee1, routee2, routee3) = CreateBroadcastGroupRouterWith3Routees(ActorSystem);

            ActorSystem.Root.Send(router, new RouterBroadcastMessage("hello"));

            Assert.Equal("hello", await ActorSystem.Root.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Equal("hello", await ActorSystem.Root.RequestAsync<string>(routee2, "received?", _timeout));
            Assert.Equal("hello", await ActorSystem.Root.RequestAsync<string>(routee3, "received?", _timeout));
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
