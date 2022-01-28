using System;
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

        [Fact]
        public async Task BroadcastGroupPool_CreatesRoutees()
        {
            await using var system = new ActorSystem();

            var props = system.Root.NewBroadcastPool(MyActorProps, 3)
                .WithMailbox(() => new TestMailbox());
            var router = system.Root.Spawn(props);
            var routees = await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.Equal(3, routees.Pids.Count);
        }

        [Fact]
        public async Task RoundRobinPool_CreatesRoutees()
        {
            await using var system = new ActorSystem();

            var props = system.Root.NewRoundRobinPool(MyActorProps, 3)
                .WithMailbox(() => new TestMailbox());
            var router = system.Root.Spawn(props);
            var routees = await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.Equal(3, routees.Pids.Count);
        }

        [Fact]
        public async Task ConsistentHashPool_CreatesRoutees()
        {
            await using var system = new ActorSystem();

            var props = system.Root.NewConsistentHashPool(MyActorProps, 3)
                .WithMailbox(() => new TestMailbox());
            var router = system.Root.Spawn(props);
            var routees = await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.Equal(3, routees.Pids.Count);
        }

        [Fact]
        public async Task RandomPool_CreatesRoutees()
        {
            await using var system = new ActorSystem();

            var props = system.Root.NewRandomPool(MyActorProps, 3, 0)
                .WithMailbox(() => new TestMailbox());
            var router = system.Root.Spawn(props);
            var routees = await system.Root.RequestAsync<Routees>(router, new RouterGetRoutees(), _timeout);
            Assert.Equal(3, routees.Pids.Count);
        }
    }
}