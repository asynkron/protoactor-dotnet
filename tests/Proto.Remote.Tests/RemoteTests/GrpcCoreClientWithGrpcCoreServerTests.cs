using Proto.Remote.GrpcCore;
using Xunit;

// ReSharper disable MethodHasAsyncOverload

namespace Proto.Remote.Tests
{
    public class GrpcCoreClientWithGrpcCoreServerTests
        : RemoteTests,
            IClassFixture<GrpcCoreClientWithGrpcCoreServerTests.Fixture>
    {
        public GrpcCoreClientWithGrpcCoreServerTests(Fixture fixture) : base(fixture)
        {
        }

        public class Fixture : RemoteFixture
        {
            public Fixture()
            {
                GrpcCoreRemoteConfig clientConfig =
                    ConfigureClientRemoteConfig(GrpcCoreRemoteConfig.BindToLocalhost(5000));
                Remote = GetGrpcCoreRemote(clientConfig);
                GrpcCoreRemoteConfig serverConfig =
                    ConfigureServerRemoteConfig(GrpcCoreRemoteConfig.BindToLocalhost(5001));
                ServerRemote = GetGrpcCoreRemote(serverConfig);
            }
        }
    }
}
