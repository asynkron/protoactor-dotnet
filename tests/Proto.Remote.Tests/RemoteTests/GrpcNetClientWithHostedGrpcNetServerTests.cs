using Proto.Remote;
using Proto.Remote.GrpcNet;
using Xunit;

// ReSharper disable MethodHasAsyncOverload

namespace Proto.Remote.Tests;

public class GrpcNetClientWithHostedGrpcNetServerTests
    : RemoteTests,
        IClassFixture<GrpcNetClientWithHostedGrpcNetServerTests.Fixture>
{
    public GrpcNetClientWithHostedGrpcNetServerTests(Fixture fixture) : base(fixture)
    {
    }

    public class Fixture : RemoteFixture
    {
        public Fixture() : base(
            Fixture(
                Client(RemoteTransportKind.GrpcNet),
                Server(RemoteTransportKind.HostedGrpcNet)
            )
        )
        {
        }
    }
}
