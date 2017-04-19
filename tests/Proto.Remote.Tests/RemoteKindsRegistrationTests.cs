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
            Remote.RegisterKnownKind(kind, props);
            Assert.Equal(props, Remote.GetKnownKind(kind));
        }

        [Fact]
        public void CanRegisterMultipleKinds()
        {
            var props = new Props();
            var kind1 = Guid.NewGuid().ToString();
            var kind2 = Guid.NewGuid().ToString();
            Remote.RegisterKnownKind(kind1, props);
            Remote.RegisterKnownKind(kind2, props);
            var kinds = Remote.GetKnownKinds();
            Assert.Contains(kind1, kinds);
            Assert.Contains(kind2, kinds);
        }

        [Fact]
        public void UnknownKindThrowsException()
        {
            Assert.Throws<ArgumentException>(() => { Remote.GetKnownKind("not registered"); });
        }
    }
}