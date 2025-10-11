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
        public Fixture() : base(
            Fixture(
                Client(RemoteTransportKind.HostedGrpcNet),
                Server(RemoteTransportKind.GrpcNet)
            )
        )
        {
        }
    }
}
