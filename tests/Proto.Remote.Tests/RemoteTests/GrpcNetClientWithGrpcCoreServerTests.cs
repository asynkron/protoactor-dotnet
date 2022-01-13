using Proto.Remote.GrpcCore;
using Proto.Remote.GrpcNet;
using Xunit;

// ReSharper disable MethodHasAsyncOverload

namespace Proto.Remote.Tests
{
    public class GrpcNetClientWithGrpcCoreServerTests
        : RemoteTests,
            IClassFixture<GrpcNetClientWithGrpcCoreServerTests.Fixture>
    {
        public GrpcNetClientWithGrpcCoreServerTests(Fixture fixture) : base(fixture)
        {
        }

        public class Fixture : RemoteFixture
        {
            public Fixture()
            {
                var clientConfig = ConfigureClientRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost(5000));
                Remote = GetGrpcNetRemote(clientConfig);
                var serverConfig = ConfigureServerRemoteConfig(GrpcCoreRemoteConfig.BindToLocalhost(5001));
                ServerRemote1 = GetGrpcCoreRemote(serverConfig);var serverConfig2 = ConfigureServerRemoteConfig(GrpcCoreRemoteConfig.BindToLocalhost(5002));
                ServerRemote2 = GetGrpcCoreRemote(serverConfig2);
            }
        }
    }
}