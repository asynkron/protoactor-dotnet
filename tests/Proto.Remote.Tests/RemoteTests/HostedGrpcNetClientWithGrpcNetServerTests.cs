using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Proto.Remote;
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
            var clientConfig = ConfigureClientRemoteConfig(RemoteConfig.BindToLocalhost());
            (_clientHost, Remote) = GetHostedGrpcNetRemote(clientConfig);
            var serverConfig = ConfigureServerRemoteConfig(RemoteConfig.BindToLocalhost());
            var serverConfig2 = ConfigureServerRemoteConfig(RemoteConfig.BindToLocalhost());
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