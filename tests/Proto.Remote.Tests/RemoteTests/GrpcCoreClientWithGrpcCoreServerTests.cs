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
                var clientConfig = ConfigureClientRemoteConfig(GrpcCoreRemoteConfig.BindToLocalhost());
                Remote = GetGrpcCoreRemote(clientConfig);
                var serverConfig = ConfigureServerRemoteConfig(GrpcCoreRemoteConfig.BindToLocalhost());
                ServerRemote1 = GetGrpcCoreRemote(serverConfig);
                var serverConfig2 = ConfigureServerRemoteConfig(GrpcCoreRemoteConfig.BindToLocalhost());
                ServerRemote2 = GetGrpcCoreRemote(serverConfig2);
            }
        }
    }
}