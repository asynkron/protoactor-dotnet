using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Proto.Remote.GrpcNet;
using Xunit;

// ReSharper disable MethodHasAsyncOverload

namespace Proto.Remote.Tests
{
    public class HostedGrpcNetClientWithHostedGrpcNetServerTests
        : RemoteTests,
            IClassFixture<HostedGrpcNetClientWithHostedGrpcNetServerTests.Fixture>
    {
        public HostedGrpcNetClientWithHostedGrpcNetServerTests(Fixture fixture) : base(fixture)
        {
        }

        public class Fixture : RemoteFixture
        {
            private readonly IHost _clientHost;
            private readonly IHost _serverHost;

            public Fixture()
            {
                var clientConfig = ConfigureClientRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost(5000));
                (_clientHost, Remote) = GetHostedGrpcNetRemote(clientConfig);
                var serverConfig = ConfigureServerRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost(5001));
                (_serverHost, ServerRemote) = GetHostedGrpcNetRemote(serverConfig);
            }

            public override async Task DisposeAsync()
            {
                await _clientHost.StopAsync();
                _clientHost.Dispose();
                await _serverHost.StopAsync();
                _serverHost.Dispose();
            }
        }
    }
}