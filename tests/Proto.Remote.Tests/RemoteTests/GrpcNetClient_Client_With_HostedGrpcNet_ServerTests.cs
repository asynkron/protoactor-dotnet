using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Proto.Remote.GrpcNet;
using Xunit;

// ReSharper disable MethodHasAsyncOverload

namespace Proto.Remote.Tests
{
    public class GrpcNetClient_Client_With_HostedGrpcNet_ServerTests
        : RemoteTests,
            IClassFixture<GrpcNetClient_Client_With_HostedGrpcNet_ServerTests.Fixture>
    {
        public GrpcNetClient_Client_With_HostedGrpcNet_ServerTests(Fixture fixture) : base(fixture)
        {
        }

        public class Fixture : RemoteFixture
        {
            private readonly IHost _serverHost;

            public Fixture()
            {
                var clientConfig = ConfigureClientRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost());
                Remote = GetGrpcNetClientRemote(clientConfig);
                var serverConfig = ConfigureServerRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost(5001));
                (_serverHost, ServerRemote) = GetHostedGrpcNetRemote(serverConfig);
            }

            public override async Task DisposeAsync()
            {
                await Remote.ShutdownAsync();
                await _serverHost.StopAsync();
                _serverHost.Dispose();
            }
        }
    }
}