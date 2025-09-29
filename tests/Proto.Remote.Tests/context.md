# Proto.Remote.Tests Context

## Overview
Remote transport test suite covering client/server combinations and serialization scenarios.

## Key Files
* `RemoteFixture.cs` – shared fixture infrastructure that now composes client/server transports through descriptors and manages lifecycle/host disposal centrally.
* `RemoteTests.cs` – base assertions verifying remote messaging, spawning, and watching behaviours using the shared fixture interface.
* `RemoteTests/` – subdirectory with concrete transport matrix tests.
* `HostedGrpcNetWithCustomeSerializerTests.cs` – validates hosted transports with a custom serializer registration.

## Primary Types and Contracts
* `IRemoteFixture` – async fixture contract exposing remotes and addresses.
* `RemoteFixture` – descriptor-driven fixture base automatically provisioning and tearing down remotes/hosts.
* `RemoteTests` – abstract test suite leveraging any `IRemoteFixture` implementation.

## Related Subcontexts
* [Remote Transport Matrix](RemoteTests/context.md) – transport combination fixtures built on the shared descriptors.
