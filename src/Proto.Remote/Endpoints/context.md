# Endpoints Context

## Overview
Endpoint management, including readers, writers, batching, and pipeline orchestration for remote messaging.

_Parent context: [Proto Remote](../context.md)_

## Key Files
* `BlockedEndpoint.cs` – C# source defining Blocked Endpoint behavior.
* `ClientConnectionMode.cs` – C# source defining Client Connection Mode behavior.
* `ClientRemoteEndpoint.cs` – C# source defining Client Remote Endpoint behavior.
* `ConnectionRunnerLogMessages.cs` – C# source defining Connection Runner Log Messages behavior.
* `EndpointManager.cs` – C# source defining Endpoint Manager behavior.
* `EndpointManagerLogMessages.cs` – C# source defining Endpoint Manager Log Messages behavior.
* `EndpointWriterOptions.cs` – C# source defining Endpoint Writer Options behavior.
* `IConnectionMode.cs` – C# source defining I Connection Mode behavior.
* `IRemoteEndpoint.cs` – C# source defining I Remote Endpoint behavior.
* `MessageBatchFactory.cs` – C# source defining Message Batch Factory behavior.
* `MessageBatchFactoryLogMessages.cs` – C# source defining Message Batch Factory Log Messages behavior.
* `README.md` – Markdown documentation.
* `RemoteEndpointBase.cs` – C# source defining Remote Endpoint Base behavior.
* `RemoteMessageHandler.cs` – C# source defining Remote Message Handler behavior.
* `RemotingGrpcService.cs` – C# source defining Remoting Grpc Service behavior.
* `ServerConnectionMode.cs` – C# source defining Server Connection Mode behavior.
* `ServerConnector.cs` – C# source defining Server Connector behavior.
* `ServerRemoteEndpoint.cs` – C# source defining Server Remote Endpoint behavior.

## Primary Types and Contracts
* `Class BlockedEndpoint` defined in `BlockedEndpoint.cs`
* `Class ClientConnectionMode` defined in `ClientConnectionMode.cs`
* `Class ClientRemoteEndpoint` defined in `ClientRemoteEndpoint.cs`
* `Class ConnectionRunnerLogMessages` defined in `ConnectionRunnerLogMessages.cs`
* `Class EndpointManager` defined in `EndpointManager.cs`
* `Class EndpointManagerLogMessages` defined in `EndpointManagerLogMessages.cs`
* `Class EndpointWriterOptions` defined in `EndpointWriterOptions.cs`
* `Interface IConnectionMode` defined in `IConnectionMode.cs`
* `Interface IRemoteEndpoint` defined in `IRemoteEndpoint.cs`
* `Class MessageBatchFactory` defined in `MessageBatchFactory.cs`
* `Class MessageBatchFactoryLogMessages` defined in `MessageBatchFactoryLogMessages.cs`
* `Class RemoteEndpointBase` defined in `RemoteEndpointBase.cs`
* `Class MultiTaskReuseWaiter` defined in `RemoteEndpointBase.cs`
* `Class RemoteMessageHandler` defined in `RemoteMessageHandler.cs`
* `Class RemotingGrpcService` defined in `RemotingGrpcService.cs`
* `Record struct` defined in `RemotingGrpcService.cs`
* `Class ServerConnectionMode` defined in `ServerConnectionMode.cs`
* `Class ServerConnector` defined in `ServerConnector.cs`
* `Enum Type` defined in `ServerConnector.cs`
* `Class ServerRemoteEndpoint` defined in `ServerRemoteEndpoint.cs`

## Related Subcontexts
* [Runner](Runner/context.md) – See the nested context for details.

