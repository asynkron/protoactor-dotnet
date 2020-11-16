using Proto.Remote.GrpcCore;
using Xunit;

// ReSharper disable MethodHasAsyncOverload

namespace Proto.Remote.Tests
{
    public class GrpcCoreClientWithGrpcCoreServerTests : RemoteTests, IClassFixture<GrpcCoreClientWithGrpcCoreServerTests.GrpcCoreClientWithGrpcCoreServerFixture>
    {
        public class GrpcCoreClientWithGrpcCoreServerFixture : RemoteFixture
        {
            public GrpcCoreClientWithGrpcCoreServerFixture()
            {
                var clientConfig = ConfigureClientRemoteConfig(GrpcCoreRemoteConfig.BindToLocalhost(5000));
                Remote = GetGrpcCoreRemote(clientConfig);
                var serverConfig = ConfigureServerRemoteConfig(GrpcCoreRemoteConfig.BindToLocalhost(5001));
                ServerRemote = GetGrpcCoreRemote(serverConfig);
            }
        }
        public GrpcCoreClientWithGrpcCoreServerTests(GrpcCoreClientWithGrpcCoreServerFixture fixture) : base(fixture)
        {
        }
    }
}