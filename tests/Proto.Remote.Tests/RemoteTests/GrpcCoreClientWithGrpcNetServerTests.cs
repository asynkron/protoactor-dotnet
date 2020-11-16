using Proto.Remote.GrpcCore;
using Proto.Remote.GrpcNet;
using Xunit;

// ReSharper disable MethodHasAsyncOverload

namespace Proto.Remote.Tests
{
    public class GrpcCoreClientWithGrpcNetServerTests : RemoteTests, IClassFixture<GrpcCoreClientWithGrpcNetServerTests.GrpcCoreClientWithGrpcNetServerFixture>
    {
        public class GrpcCoreClientWithGrpcNetServerFixture : RemoteFixture
        {
            public GrpcCoreClientWithGrpcNetServerFixture()
            {
                var clientConfig = ConfigureClientRemoteConfig(GrpcCoreRemoteConfig.BindToLocalhost(5000));
                Remote = GetGrpcCoreRemote(clientConfig);
                var serverConfig = ConfigureServerRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost(5001));
                ServerRemote = GetGrpcNetRemote(serverConfig);
            }
        }
        public GrpcCoreClientWithGrpcNetServerTests(GrpcCoreClientWithGrpcNetServerFixture fixture) : base(fixture)
        {
        }
    }
}