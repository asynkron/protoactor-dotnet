using Proto.Remote.GrpcNet;
using Xunit;

// ReSharper disable MethodHasAsyncOverload

namespace Proto.Remote.Tests
{
    public class GrpcNetClientWithGrpcNetServerTests : RemoteTests, IClassFixture<GrpcNetClientWithGrpcNetServerTests.GrpcNetClientWithGrpcNetServerFixture>
    {
        public class GrpcNetClientWithGrpcNetServerFixture : RemoteFixture
        {
            public GrpcNetClientWithGrpcNetServerFixture()
            {
                var clientConfig = ConfigureClientRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost(5000));
                Remote = GetGrpcNetRemote(clientConfig);
                var serverConfig = ConfigureServerRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost(5001));
                ServerRemote = GetGrpcNetRemote(serverConfig);
            }
        }
        public GrpcNetClientWithGrpcNetServerTests(GrpcNetClientWithGrpcNetServerFixture fixture) : base(fixture)
        {
        }
    }
}