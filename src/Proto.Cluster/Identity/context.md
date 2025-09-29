# Identity Context

## Overview
Identity directory within the Proto.Actor repository.

_Parent context: [Proto Cluster](../context.md)_

## Key Files
* `GetPid.cs` – C# source defining Get Pid behavior.
* `IIdentityLookup.cs` – C# source defining I Identity Lookup behavior.
* `IIdentityStorage.cs` – C# source defining I Identity Storage behavior.
* `IdentityActivatorProxy.cs` – C# source defining Identity Activator Proxy behavior.
* `IdentityIsBlockedException.cs` – C# source defining Identity Is Blocked Exception behavior.
* `IdentityMetrics.cs` – C# source defining Identity Metrics behavior.
* `IdentityStorageLookup.cs` – C# source defining Identity Storage Lookup behavior.
* `IdentityStoragePlacementActor.cs` – C# source defining Identity Storage Placement Actor behavior.
* `IdentityStorageWorker.cs` – C# source defining Identity Storage Worker behavior.

## Primary Types and Contracts
* `Class GetPid` defined in `GetPid.cs`
* `Class PidResult` defined in `GetPid.cs`
* `Interface IIdentityLookup` defined in `IIdentityLookup.cs`
* `Interface IIdentityStorage` defined in `IIdentityStorage.cs`
* `Class SpawnLock` defined in `IIdentityStorage.cs`
* `Class StoredActivation` defined in `IIdentityStorage.cs`
* `Class StorageFailureException` defined in `IIdentityStorage.cs`
* `Class LockNotFoundException` defined in `IIdentityStorage.cs`
* `Class IdentityActivatorProxy` defined in `IdentityActivatorProxy.cs`
* `Class IdentityIsBlockedException` defined in `IdentityIsBlockedException.cs`
* `Class IdentityMetrics` defined in `IdentityMetrics.cs`
* `Class IdentityStorageLookup` defined in `IdentityStorageLookup.cs`
* `Class IdentityStoragePlacementActor` defined in `IdentityStoragePlacementActor.cs`
* `Class IdentityStorageWorker` defined in `IdentityStorageWorker.cs`

## Related Subcontexts
* No nested subcontexts.

