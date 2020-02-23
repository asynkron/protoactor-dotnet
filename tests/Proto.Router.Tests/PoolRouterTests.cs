﻿using System;
using System.Threading.Tasks;
using Proto.Router.Messages;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Router.Tests
{
    public class PoolRouterTests
    {
        private static readonly RootContext Context = new RootContext();
        private static readonly Props MyActorProps = Props.FromProducer(() => new DoNothingActor());
        private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(1000);

        [Fact]
        public async Task BroadcastGroupPool_CreatesRoutees()
        {
            var props = Router.NewBroadcastPool(MyActorProps, 3)
                .WithMailbox(() => new TestMailbox());
            var router = Context.Spawn(props);
            var routees = await Context.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.Equal(3, routees.PIDs.Count);
        }

        [Fact]
        public async Task RoundRobinPool_CreatesRoutees()
        {
            var props = Router.NewRoundRobinPool(MyActorProps, 3)
                .WithMailbox(() => new TestMailbox());
            var router = Context.Spawn(props);
            var routees = await Context.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.Equal(3, routees.PIDs.Count);
        }

        [Fact]
        public async Task ConsistentHashPool_CreatesRoutees()
        {
            var props = Router.NewConsistentHashPool(MyActorProps, 3)
                .WithMailbox(() => new TestMailbox());
            var router = Context.Spawn(props);
            var routees = await Context.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.Equal(3, routees.PIDs.Count);
        }

        [Fact]
        public async Task RandomPool_CreatesRoutees()
        {
            var props = Router.NewRandomPool(MyActorProps, 3)
                .WithMailbox(() => new TestMailbox());
            var router = Context.Spawn(props);
            var routees = await Context.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.Equal(3, routees.PIDs.Count);
        }
    }
}
