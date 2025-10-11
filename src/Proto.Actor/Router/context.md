# Router Context

## Overview
Routing actors and their message contracts, supporting broadcast, round-robin, consistent hashing, and other routers.

_Parent context: [Proto Actor](../context.md)_

## Key Files
* `HashRing.cs` – C# source defining Hash Ring behavior.
* `IHashable.cs` – C# source defining I Hashable behavior.
* `RouterActor.cs` – C# source defining Router Actor behavior.
* `RouterExtensions.cs` – C# source defining Router Extensions behavior.
* `RouterProcess.cs` – C# source defining Router Process behavior.

## Primary Types and Contracts
* `Class HashRing` defined in `HashRing.cs`
* `Interface IHashable` defined in `IHashable.cs`
* `Class RouterActor` defined in `RouterActor.cs`
* `Class RouterExtensions` defined in `RouterExtensions.cs`
* `Class RouterProcess` defined in `RouterProcess.cs`

## Related Subcontexts
* [Messages](Messages/context.md) – See the nested context for details.
* [Routers](Routers/context.md) – See the nested context for details.

