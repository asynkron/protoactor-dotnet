using Proto.Remote;
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
            var clientConfig = ConfigureClientRemoteConfig(RemoteConfig.BindToLocalhost());
            Remote = GetGrpcNetRemote(clientConfig);
            var serverConfig = ConfigureServerRemoteConfig(RemoteConfig.BindToLocalhost());
            var serverConfig2 = ConfigureServerRemoteConfig(RemoteConfig.BindToLocalhost());
            ServerRemote1 = GetGrpcNetRemote(serverConfig);
            ServerRemote2 = GetGrpcNetRemote(serverConfig2);
        }
    }
}