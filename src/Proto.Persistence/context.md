# Proto Persistence Context

## Overview
Persistence abstractions and tooling shared across providers (journals, snapshots, strategies).

_Parent context: [Src](../context.md)_

## Key Files
* `IProvider.cs` – C# source defining I Provider behavior.
* `ISnapshotStrategy.cs` – C# source defining I Snapshot Strategy behavior.
* `Messages.cs` – C# source defining Messages behavior.
* `Persistence.cs` – C# source defining Persistence behavior.
* `Proto.Persistence.csproj` – Project file configuring compilation targets and dependencies.

## Primary Types and Contracts
* `Interface ISnapshotStore` defined in `IProvider.cs`
* `Interface IEventStore` defined in `IProvider.cs`
* `Interface IProvider` defined in `IProvider.cs`
* `Interface ISnapshotStrategy` defined in `ISnapshotStrategy.cs`
* `Class Event` defined in `Messages.cs`
* `Class PersistedSnapshot` defined in `Messages.cs`
* `Class RecoverSnapshot` defined in `Messages.cs`
* `Class Snapshot` defined in `Messages.cs`
* `Class RecoverEvent` defined in `Messages.cs`
* `Class ReplayEvent` defined in `Messages.cs`
* `Class PersistedEvent` defined in `Messages.cs`
* `Class Persistence` defined in `Persistence.cs`
* `Class ManualSnapshots` defined in `Persistence.cs`
* `Class NoEventStore` defined in `Persistence.cs`
* `Class NoSnapshotStore` defined in `Persistence.cs`

## Related Subcontexts
* [Snapshot Strategies](SnapshotStrategies/context.md) – See the nested context for details.

