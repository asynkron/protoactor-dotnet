using Proto.Remote;
using Proto.Remote.GrpcNet;
using Xunit;

// ReSharper disable MethodHasAsyncOverload

namespace Proto.Remote.Tests;

public class HostedGrpcNetClientWithHostedGrpcNetServerTests
    : RemoteTests,
        IClassFixture<HostedGrpcNetClientWithHostedGrpcNetServerTests.Fixture>
{
    public HostedGrpcNetClientWithHostedGrpcNetServerTests(Fixture fixture) : base(fixture)
    {
    }

    public class Fixture : RemoteFixture
    {
        public Fixture() : base(
            Fixture(
                Client(RemoteTransportKind.HostedGrpcNet),
                Server(RemoteTransportKind.HostedGrpcNet)
            )
        )
        {
        }
    }
}
