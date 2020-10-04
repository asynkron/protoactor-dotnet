using System;
using System.Threading.Tasks;
using Xunit;

namespace Proto.Remote.Tests
{
    public class RemoteKindsRegistrationTests
    {
        [Fact]
        public async Task CanRegisterKind()
        {
            var props = new Props();
            var kind = Guid.NewGuid().ToString();
            var remote = new Remote(new ActorSystem());
            remote.StartAsync(new RemoteConfig()
                .WithAnyHost()
                .WithAnyFreePort()
                .WithKnownKinds((kind, props))
            );
            await remote.ShutdownAsync();

            Assert.Equal(props, remote.GetKnownKind(kind));
        }

        [Fact]
        public async Task CanRegisterMultipleKinds()
        {
            var props = new Props();
            var kind1 = Guid.NewGuid().ToString();
            var kind2 = Guid.NewGuid().ToString();
            var remote = new Remote(new ActorSystem());
            remote.StartAsync(new RemoteConfig()
                .WithAnyHost()
                .WithAnyFreePort()
                .WithKnownKinds(
                    (kind1, props),
                    (kind2, props))
            );
            await remote.ShutdownAsync();

            var kinds = remote.GetKnownKinds();
            Assert.Contains(kind1, kinds);
            Assert.Contains(kind2, kinds);
        }

        [Fact]
        public async Task UnknownKindThrowsException()
        {
            var remote = new Remote(new ActorSystem());
            remote.StartAsync(new RemoteConfig().WithAnyHost().WithAnyFreePort());
            await remote.ShutdownAsync();
            
            Assert.Throws<ArgumentException>(() => { remote.GetKnownKind("not registered"); });
        }
    }
}