using Proto.Remote.GrpcCore;
using Proto.Remote.GrpcNet;
using Xunit;

// ReSharper disable MethodHasAsyncOverload

namespace Proto.Remote.Tests
{
    public class GrpcNetClientWithGrpcCoreServerTests : RemoteTests, IClassFixture<GrpcNetClientWithGrpcCoreServerTests.GrpcNetClientWithGrpcCoreServerFixture>
    {
        public class GrpcNetClientWithGrpcCoreServerFixture : RemoteFixture
        {
            public GrpcNetClientWithGrpcCoreServerFixture()
            {
                var clientConfig = ConfigureClientRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost(5000));
                Remote = GetGrpcNetRemote(clientConfig);
                var serverConfig = ConfigureServerRemoteConfig(GrpcCoreRemoteConfig.BindToLocalhost(5001));
                ServerRemote = GetGrpcCoreRemote(serverConfig);
            }
        }
        public GrpcNetClientWithGrpcCoreServerTests(GrpcNetClientWithGrpcCoreServerFixture fixture) : base(fixture)
        {

        }
    }
}