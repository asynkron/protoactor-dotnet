# Remote Transport Matrix Context

## Overview
Matrix of remote transport integration tests exercising different client/server wiring built on `RemoteFixture` descriptors.

## Key Files
* `GrpcNetClientWithGrpcNetServerTests.cs` – uses pure `GrpcNetRemote` on both sides.
* `GrpcNetClientWithHostedGrpcNetServerTests.cs` – gRPC client talking to hosted servers via the descriptor helpers.
* `GrpcNetClient_Client_With_GrpcNet_ServerTests.cs` – `GrpcNetClientRemote` against `GrpcNetRemote` servers.
* `GrpcNetClient_Client_With_HostedGrpcNet_ServerTests.cs` – client remote over gRPC with hosted servers.
* `HostedGrpcNetClientWithGrpcNetServerTests.cs` – hosted client remote to standard servers.
* `HostedGrpcNetClientWithHostedGrpcNetServerTests.cs` – hosted transports on both sides.
* `HostedGrpcNetWithCustomeSerializerTests.cs` – hosted transports using a custom serializer applied via descriptor configuration.
* `WithJsonSerializeAsDefaultTests.cs` – legacy commented-out placeholder for JSON default serializer coverage.

## Primary Types and Contracts
* `Fixture` classes in each file – declaratively specify the client/server transport descriptors; no manual host management required.

## Related Subcontexts
* [Parent Suite](../context.md) – overall remote transport testing context.
