using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Proto.Remote.GrpcCore;
using Proto.Remote.GrpcNet;
using Xunit;

// ReSharper disable MethodHasAsyncOverload

namespace Proto.Remote.Tests
{
    public class GrpcCoreClientWithHostedGrpcNetServerTests : RemoteTests, IClassFixture<GrpcCoreClientWithHostedGrpcNetServerTests.GrpcCoreClientWithHostedGrpcNetServerFixture>
    {
        public class GrpcCoreClientWithHostedGrpcNetServerFixture : RemoteFixture
        {
            private readonly IHost _serverHost;
            public GrpcCoreClientWithHostedGrpcNetServerFixture()
            {
                var clientConfig = ConfigureClientRemoteConfig(GrpcCoreRemoteConfig.BindToLocalhost(5000));
                Remote = GetGrpcCoreRemote(clientConfig);
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
        public GrpcCoreClientWithHostedGrpcNetServerTests(GrpcCoreClientWithHostedGrpcNetServerFixture fixture) : base(fixture)
        {
        }
    }
}