using Proto.Remote.GrpcNet;
using Xunit;

// ReSharper disable MethodHasAsyncOverload

namespace Proto.Remote.Tests;

public class GrpcNetClient_Client_With_HostedGrpcNet_ServerTests
    : RemoteTests,
        IClassFixture<GrpcNetClient_Client_With_HostedGrpcNet_ServerTests.Fixture>
{
    public GrpcNetClient_Client_With_HostedGrpcNet_ServerTests(Fixture fixture) : base(fixture)
    {
    }

    public class Fixture : RemoteFixture
    {
        public Fixture() : base(
            FixtureDescriptor(
                Client(RemoteTransportKind.GrpcNetClient),
                Server(RemoteTransportKind.HostedGrpcNet)
            )
        )
        {
        }
    }
}
