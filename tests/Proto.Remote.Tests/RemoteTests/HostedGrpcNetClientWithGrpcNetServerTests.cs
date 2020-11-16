using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Proto.Remote.GrpcNet;
using Xunit;

// ReSharper disable MethodHasAsyncOverload

namespace Proto.Remote.Tests
{
    public class HostedGrpcNetClientWithGrpcNetServerTests : RemoteTests, IClassFixture<HostedGrpcNetClientWithGrpcNetServerTests.HostedGrpcNetClientWithGrpcNetServerFixture>
    {
        public class HostedGrpcNetClientWithGrpcNetServerFixture : RemoteFixture
        {
            private readonly IHost _clientHost;
            public HostedGrpcNetClientWithGrpcNetServerFixture()
            {
                var clientConfig = ConfigureClientRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost(5000));
                (_clientHost, Remote) = GetHostedGrpcNetRemote(clientConfig);
                var serverConfig = ConfigureServerRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost(5001));
                ServerRemote = GetGrpcNetRemote(serverConfig);
            }
            public override async Task DisposeAsync()
            {
                await _clientHost.StopAsync();
                await ServerRemote.ShutdownAsync();
                _clientHost.Dispose();
            }
        }
        public HostedGrpcNetClientWithGrpcNetServerTests(HostedGrpcNetClientWithGrpcNetServerFixture fixture) : base(fixture)
        {
        }
    }
}