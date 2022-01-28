using System;
using System.Threading.Tasks;
using Proto.Router.Messages;
using Proto.TestFixtures;
using Proto.Tests;
using Xunit;

namespace Proto.Router.Tests
{
    public class PoolRouterTests : ActorTestBase
    {
        private static readonly Props MyActorProps = Props.FromProducer(() => new DoNothingActor());
        private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(1000);

        [Fact]
        public async Task BroadcastGroupPool_CreatesRoutees()
        {
            var props =  System.Root.NewBroadcastPool(MyActorProps, 3)
                .WithMailbox(() => new TestMailbox());
            var router = System.Root.Spawn(props);
            var routees = await System.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.Equal(3, routees.Pids.Count);
        }

        [Fact]
        public async Task RoundRobinPool_CreatesRoutees()
        {
            var props =  System.Root.NewRoundRobinPool(MyActorProps, 3)
                .WithMailbox(() => new TestMailbox());
            var router = System.Root.Spawn(props);
            var routees = await System.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.Equal(3, routees.Pids.Count);
        }

        [Fact]
        public async Task ConsistentHashPool_CreatesRoutees()
        {
            var props =  System.Root.NewConsistentHashPool(MyActorProps, 3)
                .WithMailbox(() => new TestMailbox());
            var router = System.Root.Spawn(props);
            var routees = await System.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.Equal(3, routees.Pids.Count);
        }

        [Fact]
        public async Task RandomPool_CreatesRoutees()
        {
            var props =  System.Root.NewRandomPool(MyActorProps, 3, 0)
                .WithMailbox(() => new TestMailbox());
            var router = System.Root.Spawn(props);
            var routees = await System.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.Equal(3, routees.Pids.Count);
        }
    }
}