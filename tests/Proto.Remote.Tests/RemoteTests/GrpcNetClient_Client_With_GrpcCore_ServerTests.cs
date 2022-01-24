using Proto.Remote.GrpcCore;
using Proto.Remote.GrpcNet;
using Xunit;

// ReSharper disable MethodHasAsyncOverload

namespace Proto.Remote.Tests
{
    public class GrpcNetClient_Client_With_GrpcCore_ServerTests
        : RemoteTests,
            IClassFixture<GrpcNetClient_Client_With_GrpcCore_ServerTests.Fixture>
    {
        public GrpcNetClient_Client_With_GrpcCore_ServerTests(Fixture fixture) : base(fixture)
        {
        }

        public class Fixture : RemoteFixture
        {
            public Fixture()
            {
                var clientConfig = ConfigureClientRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost());
                Remote = GetGrpcNetClientRemote(clientConfig);
                var serverConfig = ConfigureServerRemoteConfig(GrpcCoreRemoteConfig.BindToLocalhost());
                ServerRemote1 = GetGrpcCoreRemote(serverConfig);
                var serverConfig2 = ConfigureServerRemoteConfig(GrpcCoreRemoteConfig.BindToLocalhost());
                ServerRemote2 = GetGrpcCoreRemote(serverConfig2);
            }
        }
    }
}