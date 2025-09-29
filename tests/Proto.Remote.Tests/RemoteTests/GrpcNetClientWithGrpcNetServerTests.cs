using Proto.Remote.GrpcNet;
using Xunit;

// ReSharper disable MethodHasAsyncOverload

namespace Proto.Remote.Tests;

public class GrpcNetServerClientWithGrpcNetServerTests
    : RemoteTests,
        IClassFixture<GrpcNetServerClientWithGrpcNetServerTests.Fixture>
{
    public GrpcNetServerClientWithGrpcNetServerTests(Fixture fixture) : base(fixture)
    {
    }

    public class Fixture : RemoteFixture
    {
        public Fixture() : base(
            FixtureDescriptor(
                Client(RemoteTransportKind.GrpcNet),
                Server(RemoteTransportKind.GrpcNet)
            )
        )
        {
        }
    }
}
