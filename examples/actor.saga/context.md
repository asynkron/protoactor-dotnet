# Actor Saga Context

## Overview
Saga orchestration example with state machines.

_Parent context: [Examples](../context.md)_

## Key Files
* `Account.cs` – C# source defining Account behavior.
* `AccountProxy.cs` – C# source defining Account Proxy behavior.
* `InMemoryProvider.cs` – C# source defining In Memory Provider behavior.
* `Program.cs` – C# source defining Program behavior.
* `README.md` – Markdown documentation.
* `Runner.cs` – C# source defining Runner behavior.
* `Saga.csproj` – Project file configuring compilation targets and dependencies.
* `TransferProcess.cs` – C# source defining Transfer Process behavior.

## Primary Types and Contracts
* `Class Account` defined in `Account.cs`
* `Enum Behavior` defined in `Account.cs`
* `Class AccountProxy` defined in `AccountProxy.cs`
* `Class InMemoryProvider` defined in `InMemoryProvider.cs`
* `Class Program` defined in `Program.cs`
* `Class Runner` defined in `Runner.cs`
* `Class TransferProcess` defined in `TransferProcess.cs`

## Related Subcontexts
* [Factories](Factories/context.md) – See the nested context for details.
* [Internal](Internal/context.md) – See the nested context for details.
* [Messages](Messages/context.md) – See the nested context for details.

