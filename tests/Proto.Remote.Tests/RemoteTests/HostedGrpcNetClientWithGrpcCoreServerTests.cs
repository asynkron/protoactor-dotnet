using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Proto.Remote.GrpcCore;
using Proto.Remote.GrpcNet;
using Xunit;

// ReSharper disable MethodHasAsyncOverload

namespace Proto.Remote.Tests
{
    public class HostedGrpcNetClientWithGrpcCoreServerTests
        : RemoteTests,
            IClassFixture<HostedGrpcNetClientWithGrpcCoreServerTests.Fixture>
    {
        public HostedGrpcNetClientWithGrpcCoreServerTests(Fixture fixture) : base(fixture)
        {
        }

        public class Fixture : RemoteFixture
        {
            private readonly IHost _clientHost;

            public Fixture()
            {
                var clientConfig = ConfigureClientRemoteConfig(GrpcNetRemoteConfig.BindToLocalhost());
                (_clientHost, Remote) = GetHostedGrpcNetRemote(clientConfig);
                var serverConfig = ConfigureServerRemoteConfig(GrpcCoreRemoteConfig.BindToLocalhost());
                ServerRemote1 = GetGrpcCoreRemote(serverConfig);var serverConfig2 = ConfigureServerRemoteConfig(GrpcCoreRemoteConfig.BindToLocalhost());
                ServerRemote2 = GetGrpcCoreRemote(serverConfig2);
            }

            public override async Task DisposeAsync()
            {
                await _clientHost.StopAsync();
                _clientHost.Dispose();
                await ServerRemote1.ShutdownAsync();
            }
        }
    }
}