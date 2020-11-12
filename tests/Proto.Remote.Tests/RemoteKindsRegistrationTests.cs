using System;
using Xunit;

namespace Proto.Remote.Tests
{
    public class RemoteKindsRegistrationTests
    {
        [Fact]
        public void CanRegisterKind()
        {
            var props = new Props();
            var kind = Guid.NewGuid().ToString();
            var remote = new Remote(new ActorSystem(), 
                 RemoteConfig.BindToLocalhost()
                .WithRemoteKinds((kind, props))
            );

            Assert.Equal(props, remote.GetRemoteKind(kind));
        }

        [Fact]
        public void CanRegisterMultipleKinds()
        {
            var props = new Props();
            var kind1 = Guid.NewGuid().ToString();
            var kind2 = Guid.NewGuid().ToString();
            var remote = new Remote(new ActorSystem(),
                RemoteConfig
                    .BindToLocalhost()
                    .WithRemoteKinds(
                        (kind1, props),
                        (kind2, props)
                    )
            );

            var kinds = remote.GetRemoteKinds();
            Assert.Contains(kind1, kinds);
            Assert.Contains(kind2, kinds);
        }

        [Fact]
        public void UnknownKindThrowsException()
        {
            var remote = new Remote(new ActorSystem(), RemoteConfig.BindToLocalhost());

            Assert.Throws<ArgumentException>(() => { remote.GetRemoteKind("not registered"); });
        }
    }
}