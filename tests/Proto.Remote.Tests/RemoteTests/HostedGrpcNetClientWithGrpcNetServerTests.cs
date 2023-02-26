using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Proto.Remote.GrpcNet;
using Xunit;

// ReSharper disable MethodHasAsyncOverload

namespace Proto.Remote.Tests;

public class HostedGrpcNetClientWithGrpcNetServerTests
    : RemoteTests,
        IClassFixture<HostedGrpcNetClientWithGrpcNetServerTests.Fixture>
{
    public HostedGrpcNetClientWithGrpcNetServerTests(Fixture fixture) : base(fixture)
    {
    }

    public class Fixture : RemoteFixture
    {
        private readonly IHost _clientHost;

        public Fixture()
        {
            var clientConfig = ConfigureClientRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost());
            (_clientHost, Remote) = GetHostedGrpcNetRemote(clientConfig);
            var serverConfig = ConfigureServerRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost());
            var serverConfig2 = ConfigureServerRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost());
            ServerRemote1 = GetGrpcNetRemote(serverConfig);
            ServerRemote2 = GetGrpcNetRemote(serverConfig2);
        }

        public override async Task DisposeAsync()
        {
            await _clientHost.StopAsync();
            _clientHost.Dispose();
            await ServerRemote1.ShutdownAsync();
        }
    }
}