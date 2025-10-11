using Proto.Remote;
using Proto.Remote.GrpcNet;
using Xunit;

// ReSharper disable MethodHasAsyncOverload

namespace Proto.Remote.Tests;

public class GrpcNetClient_Client_With_GrpcNet_ServerTests
    : RemoteTests,
        IClassFixture<GrpcNetClient_Client_With_GrpcNet_ServerTests.Fixture>
{
    public GrpcNetClient_Client_With_GrpcNet_ServerTests(Fixture fixture) : base(fixture)
    {
    }

    public class Fixture : RemoteFixture
    {
        public Fixture() : base(
            Fixture(
                Client(RemoteTransportKind.GrpcNetClient),
                Server(RemoteTransportKind.GrpcNet)
            )
        )
        {
        }
    }
}
