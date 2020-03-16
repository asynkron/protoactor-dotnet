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
            var remote = new Remote(new ActorSystem(), new Serialization());
            remote.RegisterKnownKind(kind, props);
            Assert.Equal(props, remote.GetKnownKind(kind));
        }

        [Fact]
        public void CanRegisterMultipleKinds()
        {
            var props = new Props();
            var kind1 = Guid.NewGuid().ToString();
            var kind2 = Guid.NewGuid().ToString();
            var remote = new Remote(new ActorSystem(), new Serialization());
            remote.RegisterKnownKind(kind1, props);
            remote.RegisterKnownKind(kind2, props);
            var kinds = remote.GetKnownKinds();
            Assert.Contains(kind1, kinds);
            Assert.Contains(kind2, kinds);
        }

        [Fact]
        public void UnknownKindThrowsException()
        {
            var remote = new Remote(new ActorSystem(), new Serialization());
            Assert.Throws<ArgumentException>(() => { remote.GetKnownKind("not registered"); });
        }
    }
}