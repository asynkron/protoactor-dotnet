using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Proto.Remote.GrpcNet;
using Xunit;

// ReSharper disable MethodHasAsyncOverload

namespace Proto.Remote.Tests;

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
        private readonly IHost _serverHost2;

        public Fixture()
        {
            var clientConfig = ConfigureClientRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost());
            Remote = GetGrpcNetClientRemote(clientConfig);
            var serverConfig = ConfigureServerRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost());
            (_serverHost, ServerRemote1) = GetHostedGrpcNetRemote(serverConfig);
            var serverConfig2 = ConfigureServerRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost());
            (_serverHost2, ServerRemote2) = GetHostedGrpcNetRemote(serverConfig2);
        }

        public override async Task DisposeAsync()
        {
            await Remote.ShutdownAsync();
            await _serverHost.StopAsync();
            _serverHost.Dispose();
            await _serverHost2.StopAsync();
            _serverHost2.Dispose();
        }
    }
}