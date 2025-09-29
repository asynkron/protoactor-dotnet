# Proto Remote Context

## Overview
Remote messaging transport built on gRPC. Manages endpoints, serialization, and cluster communication plumbing.

_Parent context: [Src](../context.md)_

## Key Files
* `Activator.cs` – C# source defining Activator behavior.
* `BlockList.cs` – C# source defining Block List behavior.
* `Extensions.cs` – C# source defining Extensions behavior.
* `IBlockList.cs` – C# source defining I Block List behavior.
* `IRemote.cs` – C# source defining I Remote behavior.
* `IRemoteExtensions.cs` – C# source defining I Remote Extensions behavior.
* `IRemotePriorityMessage.cs` – C# source defining I Remote Priority Message behavior.
* `InternalsVisibleTo.cs` – C# source defining Internals Visible To behavior.
* `Messages.cs` – C# source defining Messages behavior.
* `PidExtensions.cs` – C# source defining Pid Extensions behavior.
* `Proto.Remote.csproj` – Project file configuring compilation targets and dependencies.
* `Protos.proto` – Protocol Buffers schema.
* `RemoteConfig.cs` – C# source defining Remote Config behavior.
* `RemoteConfigExtensions.cs` – C# source defining Remote Config Extensions behavior.
* `RemoteProcess.cs` – C# source defining Remote Process behavior.
* `ResponseStatusCode.cs` – C# source defining Response Status Code behavior.

## Primary Types and Contracts
* `Class Activator` defined in `Activator.cs`
* `Record MemberBlocked` defined in `BlockList.cs`
* `Class BlockList` defined in `BlockList.cs`
* `Class ActorSystemExtensions` defined in `Extensions.cs`
* `Interface IBlockList` defined in `IBlockList.cs`
* `Interface IRemote` defined in `IRemote.cs`
* `Class IRemoteExtensions` defined in `IRemoteExtensions.cs`
* `Interface IRemotePriorityMessage` defined in `IRemotePriorityMessage.cs`
* `Record EndpointTerminatedEvent` defined in `Messages.cs`
* `Record RemoteDeliver` defined in `Messages.cs`
* `Class PidExtensions` defined in `PidExtensions.cs`
* `Record RemoteConfig` defined in `RemoteConfig.cs`
* `Class RemoteConfigExtensions` defined in `RemoteConfigExtensions.cs`
* `Class RemoteProcess` defined in `RemoteProcess.cs`
* `Enum ResponseStatusCode` defined in `ResponseStatusCode.cs`

## Related Subcontexts
* [Endpoints](Endpoints/context.md) – See the nested context for details.
* [Grpc Net](GrpcNet/context.md) – See the nested context for details.
* [Healthchecks](Healthchecks/context.md) – See the nested context for details.
* [Metrics](Metrics/context.md) – See the nested context for details.
* [Properties](Properties/context.md) – See the nested context for details.
* [Serialization](Serialization/context.md) – See the nested context for details.

