﻿using System;
using System.Threading.Tasks;
using Proto.Router.Messages;
using Proto.TestFixtures;
using Proto.Tests;
using Xunit;

namespace Proto.Router.Tests
{
    public class RandomGroupRouterTests
    {
        private static readonly Props MyActorProps = Props.FromProducer(() => new MyTestActor());
        private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(1000);

        [Fact]
        public async Task RandomGroupRouter_RouteesReceiveMessagesInRandomOrder()
        {
                    await using var System = new ActorSystem();
                    var Context = System.Root;

            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees(System);

            System.Root.Send(router, "1");
            System.Root.Send(router, "2");
            System.Root.Send(router, "3");

            Assert.Equal("2", await System.Root.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Equal("3", await System.Root.RequestAsync<string>(routee2, "received?", _timeout));
            Assert.Equal("1", await System.Root.RequestAsync<string>(routee3, "received?", _timeout));
        }

        [Fact]
        public async Task RandomGroupRouter_NewlyAddedRouteesReceiveMessages()
        {
                    await using var System = new ActorSystem();
                    var Context = System.Root;

            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees(System);
            var routee4 = System.Root.Spawn(MyActorProps);
            System.Root.Send(router, new RouterAddRoutee(routee4));
            await Task.Delay(500);
            System.Root.Send(router, "1");
            System.Root.Send(router, "2");
            System.Root.Send(router, "3");
            System.Root.Send(router, "4");

            // results are random! (but consistent due to seeding) As MyTestActor only stores the most
            // recent message, "1" is overwritten by a subsequent message. 
            Assert.Equal("2", await System.Root.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Null(await System.Root.RequestAsync<string>(routee2, "received?", _timeout));
            Assert.Equal("3", await System.Root.RequestAsync<string>(routee3, "received?", _timeout));
            Assert.Equal("4", await System.Root.RequestAsync<string>(routee4, "received?", _timeout));
        }

        [Fact]
        public async Task RandomGroupRouter_RemovedRouteesDoNotReceiveMessages()
        {
                    await using var System = new ActorSystem();
                    var Context = System.Root;

            var (router, routee1, _, _) = CreateRouterWith3Routees(System);

            System.Root.Send(router, new RouterRemoveRoutee(routee1));

            for (var i = 0; i < 100; i++)
            {
                System.Root.Send(router, i.ToString());
            }

            Assert.Null(await System.Root.RequestAsync<string>(routee1, "received?", _timeout));
        }

        [Fact]
        public async Task RandomGroupRouter_RouteesCanBeRemoved()
        {
                    await using var System = new ActorSystem();
                    var Context = System.Root;

            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees(System);

            System.Root.Send(router, new RouterRemoveRoutee(routee1));

            var routees = await System.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.DoesNotContain(routee1, routees.Pids);
            Assert.Contains(routee2, routees.Pids);
            Assert.Contains(routee3, routees.Pids);
        }

        [Fact]
        public async Task RandomGroupRouter_RouteesCanBeAdded()
        {
                    await using var System = new ActorSystem();
                    var Context = System.Root;

            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees(System);
            var routee4 = System.Root.Spawn(MyActorProps);
            System.Root.Send(router, new RouterAddRoutee(routee4));

            var routees = await System.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.Contains(routee1, routees.Pids);
            Assert.Contains(routee2, routees.Pids);
            Assert.Contains(routee3, routees.Pids);
            Assert.Contains(routee4, routees.Pids);
        }

        [Fact]
        public async Task RandomGroupRouter_AllRouteesReceiveRouterBroadcastMessages()
        {
                    await using var System = new ActorSystem();
                    var Context = System.Root;

            var (router, routee1, routee2, routee3) = CreateRouterWith3Routees(System);

            System.Root.Send(router, new RouterBroadcastMessage("hello"));

            Assert.Equal("hello", await System.Root.RequestAsync<string>(routee1, "received?", _timeout));
            Assert.Equal("hello", await System.Root.RequestAsync<string>(routee2, "received?", _timeout));
            Assert.Equal("hello", await System.Root.RequestAsync<string>(routee3, "received?", _timeout));
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

        private class MyTestActor : IActor
        {
            private string? _received;

            public Task ReceiveAsync(IContext context)
            {
                switch (context.Message)
                {
                    case "received?":
                        context.Respond(_received!);
                        break;
                    case string msg:
                        _received = msg;
                        break;
                }

                return Task.CompletedTask;
            }
        }
    }
}