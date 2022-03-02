using Proto.Remote.GrpcNet;
using Xunit;

// ReSharper disable MethodHasAsyncOverload

namespace Proto.Remote.Tests;

public class GrpcNetServerClientWithGrpcNetServerTests
    : RemoteTests,
        IClassFixture<GrpcNetServerClientWithGrpcNetServerTests.Fixture>
{
    public GrpcNetServerClientWithGrpcNetServerTests(Fixture fixture) : base(fixture)
    {
    }

    public class Fixture : RemoteFixture
    {
        public Fixture()
        {
            var clientConfig = ConfigureClientRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost());
            Remote = GetGrpcNetRemote(clientConfig);
            var serverConfig = ConfigureServerRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost());
            var serverConfig2 = ConfigureServerRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost());
            ServerRemote1 = GetGrpcNetRemote(serverConfig);
            ServerRemote2 = GetGrpcNetRemote(serverConfig2);
        }
    }
}