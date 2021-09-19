﻿using System;
using System.Threading.Tasks;
using Proto.Router.Messages;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Router.Tests
{
    public class PoolRouterTests
    {
        private static readonly Props MyActorProps = Props.FromProducer(() => new DoNothingActor());
        private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(1000);
        private readonly ActorSystem ActorSystem = new();

        [Fact]
        public async Task BroadcastGroupPool_CreatesRoutees()
        {
            var props = new ActorSystem().Root.NewBroadcastPool(MyActorProps, 3)
                .WithMailbox(() => new TestMailbox());
            var router = ActorSystem.Root.Spawn(props);
            var routees = await ActorSystem.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.Equal(3, routees.Pids.Count);
        }

        [Fact]
        public async Task RoundRobinPool_CreatesRoutees()
        {
            var props = new ActorSystem().Root.NewRoundRobinPool(MyActorProps, 3)
                .WithMailbox(() => new TestMailbox());
            var router = ActorSystem.Root.Spawn(props);
            var routees = await ActorSystem.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.Equal(3, routees.Pids.Count);
        }

        [Fact]
        public async Task ConsistentHashPool_CreatesRoutees()
        {
            var props = new ActorSystem().Root.NewConsistentHashPool(MyActorProps, 3)
                .WithMailbox(() => new TestMailbox());
            var router = ActorSystem.Root.Spawn(props);
            var routees = await ActorSystem.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.Equal(3, routees.Pids.Count);
        }

        [Fact]
        public async Task RandomPool_CreatesRoutees()
        {
            var props = new ActorSystem().Root.NewRandomPool(MyActorProps, 3, 0)
                .WithMailbox(() => new TestMailbox());
            var router = ActorSystem.Root.Spawn(props);
            var routees = await ActorSystem.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.Equal(3, routees.Pids.Count);
        }
    }
}