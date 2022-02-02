using Proto.Remote.GrpcNet;
using Xunit;

// ReSharper disable MethodHasAsyncOverload

namespace Proto.Remote.Tests
{
    public class GrpcNetClient_Client_With_GrpcNet_ServerTests
        : RemoteTests,
            IClassFixture<GrpcNetClient_Client_With_GrpcNet_ServerTests.Fixture>
    {
        public GrpcNetClient_Client_With_GrpcNet_ServerTests(Fixture fixture) : base(fixture)
        {
        }

        public class Fixture : RemoteFixture
        {
            public Fixture()
            {
                var clientConfig = ConfigureClientRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost());
                Remote = GetGrpcNetClientRemote(clientConfig);
                var serverConfig = ConfigureServerRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost());
                var serverConfig2 = ConfigureServerRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost());
                ServerRemote1 = GetGrpcNetRemote(serverConfig);
                ServerRemote2 = GetGrpcNetRemote(serverConfig2);
            }
        }
    }
}