# Proto Persistence Tests Context

## Overview
Persistence subsystem tests covering journals, snapshots, and strategies.

_Parent context: [Tests](../context.md)_

## Key Files
* `AssemblyInfo.cs` – C# source defining Assembly Info behavior.
* `ContainersFixture.cs` – C# source defining Containers Fixture behavior.
* `ExamplePersistentActorTests.cs` – C# source defining Example Persistent Actor Tests behavior.
* `InMemoryProvider.cs` – C# source defining In Memory Provider behavior.
* `PersistenceWithSnapshotStrategiesTests.cs` – C# source defining Persistence With Snapshot Strategies Tests behavior.
* `Proto.Persistence.Tests.csproj` – Project file configuring compilation targets and dependencies.

## Primary Types and Contracts
* `Class ContainersFixture` defined in `ContainersFixture.cs`
* `Class ExamplePersistentActorTests` defined in `ExamplePersistentActorTests.cs`
* `Class State` defined in `ExamplePersistentActorTests.cs`
* `Enum TestProvider` defined in `ExamplePersistentActorTests.cs`
* `Class GetState` defined in `ExamplePersistentActorTests.cs`
* `Class GetIndex` defined in `ExamplePersistentActorTests.cs`
* `Class Multiply` defined in `ExamplePersistentActorTests.cs`
* `Class Multiplied` defined in `ExamplePersistentActorTests.cs`
* `Class RequestSnapshot` defined in `ExamplePersistentActorTests.cs`
* `Class ExamplePersistentActor` defined in `ExamplePersistentActorTests.cs`
* `Class InMemoryProvider` defined in `InMemoryProvider.cs`
* `Class PersistenceWithSnapshotStrategiesTests` defined in `PersistenceWithSnapshotStrategiesTests.cs`

## Related Subcontexts
* [Snapshot Strategies](SnapshotStrategies/context.md) – See the nested context for details.

