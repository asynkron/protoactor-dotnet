using Proto.Remote.GrpcCore;
using Proto.Remote.GrpcNet;
using Xunit;

// ReSharper disable MethodHasAsyncOverload

namespace Proto.Remote.Tests
{
    public class GrpcCoreClientWithGrpcNetServerTests
        : RemoteTests,
            IClassFixture<GrpcCoreClientWithGrpcNetServerTests.Fixture>
    {
        public GrpcCoreClientWithGrpcNetServerTests(Fixture fixture) : base(fixture)
        {
        }

        public class Fixture : RemoteFixture
        {
            public Fixture()
            {
                var clientConfig = ConfigureClientRemoteConfig(GrpcCoreRemoteConfig.BindToLocalhost());
                Remote = GetGrpcCoreRemote(clientConfig);
                var serverConfig = ConfigureServerRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost());
                var serverConfig2 = ConfigureServerRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost());
                ServerRemote1 = GetGrpcNetRemote(serverConfig);
                ServerRemote2 = GetGrpcNetRemote(serverConfig2);
            }
        }
    }
}