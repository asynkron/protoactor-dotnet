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
        private static readonly Props MyActorProps = Actor.FromProducer(() => new MyTestActor());
        private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(1000);

        [Fact]
        public async Task RandomGroupRouter_RouteesReceiveMessagesInRandomOrder()
        {
            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees();

            await router.SendAsync("1");
            await router.SendAsync("2");
            await router.SendAsync("3");

            Assert.Equal("2", await routee1.RequestAsync<string>("received?", _timeout));
            Assert.Equal("3", await routee2.RequestAsync<string>("received?", _timeout));
            Assert.Equal("1", await routee3.RequestAsync<string>("received?", _timeout));
        }

        [Fact]
        public async Task RandomGroupRouter_NewlyAddedRouteesReceiveMessages()
        {
            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees();
            var routee4 = Actor.Spawn(MyActorProps);
            await router.SendAsync(new RouterAddRoutee
            {
                PID = routee4
            });
            await router.SendAsync("1");
            await router.SendAsync("2");
            await router.SendAsync("3");
            await router.SendAsync("4");

            // results are random! (but consistent due to seeding) As MyTestActor only stores the most
            // recent message, "1" is overwritten by a subsequent message. 
            Assert.Equal("2", await routee1.RequestAsync<string>("received?", _timeout));
            Assert.Equal(null, await routee2.RequestAsync<string>("received?", _timeout));
            Assert.Equal("3", await routee3.RequestAsync<string>("received?", _timeout));
            Assert.Equal("4", await routee4.RequestAsync<string>("received?", _timeout));
        }

        [Fact]
        public async Task RandomGroupRouter_RemovedRouteesDoNotReceiveMessages()
        {
            var (router, routee1, _, _) = CreateRouterWith3Routees();
            await router.SendAsync(new RouterRemoveRoutee
            {
                PID = routee1
            });
            for (int i = 0; i < 100; i++)
            {
                await router.SendAsync(i.ToString());
            }
            
            Assert.Equal(null, await routee1.RequestAsync<string>("received?", _timeout));
        }

        [Fact]
        public async Task RandomGroupRouter_RouteesCanBeRemoved()
        {
            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees();

            await router.SendAsync(new RouterRemoveRoutee { PID = routee1 });

            var routees = await router.RequestAsync<Routees>(new RouterGetRoutees(), _timeout);
            Assert.DoesNotContain(routee1, routees.PIDs);
            Assert.Contains(routee2, routees.PIDs);
            Assert.Contains(routee3, routees.PIDs);
        }

        [Fact]
        public async Task RandomGroupRouter_RouteesCanBeAdded()
        {
            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees();
            var routee4 = Actor.Spawn(MyActorProps);
            await router.SendAsync(new RouterAddRoutee { PID = routee4 });

            var routees = await router.RequestAsync<Routees>(new RouterGetRoutees(), _timeout);
            Assert.Contains(routee1, routees.PIDs);
            Assert.Contains(routee2, routees.PIDs);
            Assert.Contains(routee3, routees.PIDs);
            Assert.Contains(routee4, routees.PIDs);
        }

        [Fact]
        public async Task RandomGroupRouter_AllRouteesReceiveRouterBroadcastMessages()
        {
            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees();

            await router.SendAsync(new RouterBroadcastMessage { Message = "hello" });

            Assert.Equal("hello", await routee1.RequestAsync<string>("received?", _timeout));
            Assert.Equal("hello", await routee2.RequestAsync<string>("received?", _timeout));
            Assert.Equal("hello", await routee3.RequestAsync<string>("received?", _timeout));
        }

        private (PID router, PID routee1, PID routee2, PID routee3) CreateRouterWith3Routees()
        {
            var routee1 = Actor.Spawn(MyActorProps);
            var routee2 = Actor.Spawn(MyActorProps);
            var routee3 = Actor.Spawn(MyActorProps);

            var props = Router.NewRandomGroup(MyActorProps, 10000, routee1, routee2, routee3)
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
                        return context.Sender.SendAsync(_received);
                    case string msg:
                        _received = msg;
                        break;
                }
                return Actor.Done;
            }
        }
    }
}
