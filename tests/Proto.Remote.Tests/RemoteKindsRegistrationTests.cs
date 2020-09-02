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
            var remoteKindRegistry = new RemoteKindRegistry();
            remoteKindRegistry.RegisterKnownKind(kind, props);
            Assert.Equal(props, remoteKindRegistry.GetKnownKind(kind));
        }

        [Fact]
        public void CanRegisterMultipleKinds()
        {
            var props = new Props();
            var kind1 = Guid.NewGuid().ToString();
            var kind2 = Guid.NewGuid().ToString();
             var remoteKindRegistry = new RemoteKindRegistry();
            remoteKindRegistry.RegisterKnownKind(kind1, props);
            remoteKindRegistry.RegisterKnownKind(kind2, props);
            var kinds = remoteKindRegistry.GetKnownKinds();
            Assert.Contains(kind1, kinds);
            Assert.Contains(kind2, kinds);
        }

        [Fact]
        public void UnknownKindThrowsException()
        {
            var remoteKindRegistry = new RemoteKindRegistry();
            Assert.Throws<ArgumentException>(() => { remoteKindRegistry.GetKnownKind("not registered"); });
        }
    }
}