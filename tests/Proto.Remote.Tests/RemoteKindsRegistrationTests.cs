using System;
using Proto.Remote.GrpcCore;
using Xunit;

namespace Proto.Remote.Tests
{
    public class RemoteKindsRegistrationTests
    {
        [Fact]
        public void CanRegisterKind()
        {
            Props props = new Props();
            string kind = Guid.NewGuid().ToString();
            GrpcCoreRemote remote = new GrpcCoreRemote(new ActorSystem(),
                GrpcCoreRemoteConfig.BindToLocalhost()
                    .WithRemoteKinds((kind, props))
            );

            Assert.Equal(props, remote.Config.GetRemoteKind(kind));
        }

        [Fact]
        public void CanRegisterMultipleKinds()
        {
            Props props = new Props();
            string kind1 = Guid.NewGuid().ToString();
            string kind2 = Guid.NewGuid().ToString();
            GrpcCoreRemote remote = new GrpcCoreRemote(new ActorSystem(),
                GrpcCoreRemoteConfig
                    .BindToLocalhost()
                    .WithRemoteKinds(
                        (kind1, props),
                        (kind2, props)
                    )
            );

            string[] kinds = remote.Config.GetRemoteKinds();
            Assert.Contains(kind1, kinds);
            Assert.Contains(kind2, kinds);
        }

        [Fact]
        public void UnknownKindThrowsException()
        {
            GrpcCoreRemote remote = new GrpcCoreRemote(new ActorSystem(), GrpcCoreRemoteConfig.BindToLocalhost());

            Assert.Throws<ArgumentException>(() => { remote.Config.GetRemoteKind("not registered"); });
        }
    }
}
