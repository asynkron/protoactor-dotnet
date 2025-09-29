# Proto Actor Context

## Overview
Core actor runtime implementation: actors, contexts, mailboxes, supervision, logging, scheduling, and supporting utilities.

_Parent context: [Src](../context.md)_

## Key Files
* `ActorSystem.cs` – C# source defining Actor System behavior.
* `ActorSystemConfig.cs` – C# source defining Actor System Config behavior.
* `ActorSystemLogMessages.cs` – C# source defining Actor System Log Messages behavior.
* `Behavior.cs` – C# source defining Behavior behavior.
* `CancellationTokens.cs` – C# source defining Cancellation Tokens behavior.
* `Delegates.cs` – C# source defining Delegates behavior.
* `Exceptions.cs` – C# source defining Exceptions behavior.
* `Extensions.cs` – C# source defining Extensions behavior.
* `FunctionActor.cs` – C# source defining Function Actor behavior.
* `IActor.cs` – C# source defining I Actor behavior.
* `InternalsVisibleTo.cs` – C# source defining Internals Visible To behavior.
* `Middleware.cs` – C# source defining Middleware behavior.
* `PID.cs` – C# source defining PID behavior.
* `Proto.Actor.csproj` – Project file configuring compilation targets and dependencies.
* `ProtoTags.cs` – C# source defining Proto Tags behavior.
* `Protos.proto` – Protocol Buffers schema.
* `Stopper.cs` – C# source defining Stopper behavior.

## Primary Types and Contracts
* `Class ActorSystem` defined in `ActorSystem.cs`
* `Record ActorSystemConfig` defined in `ActorSystemConfig.cs`
* `Class ActorSystemLogMessages` defined in `ActorSystemLogMessages.cs`
* `Class Behavior` defined in `Behavior.cs`
* `Class CancellationTokens` defined in `CancellationTokens.cs`
* `Record TokenEntry` defined in `CancellationTokens.cs`
* `Class ProcessNameExistException` defined in `Exceptions.cs`
* `Class UtilExtensions` defined in `Extensions.cs`
* `Class FunctionActor` defined in `FunctionActor.cs`
* `Interface IActor` defined in `IActor.cs`
* `Class Middleware` defined in `Middleware.cs`
* `Class PID` defined in `PID.cs`
* `Class ProtoTags` defined in `ProtoTags.cs`
* `Class Stopper` defined in `Stopper.cs`

## Related Subcontexts
* [Context](Context/context.md) – See the nested context for details.
* [Deduplication](Deduplication/context.md) – See the nested context for details.
* [Dependency Injection](DependencyInjection/context.md) – See the nested context for details.
* [Diagnostics](Diagnostics/context.md) – See the nested context for details.
* [Event Stream](EventStream/context.md) – See the nested context for details.
* [Extensions](Extensions/context.md) – See the nested context for details.
* [Future](Future/context.md) – See the nested context for details.
* [Logging](Logging/context.md) – See the nested context for details.
* [Mailbox](Mailbox/context.md) – See the nested context for details.
* [Messages](Messages/context.md) – See the nested context for details.
* [Metrics](Metrics/context.md) – See the nested context for details.
* [Process](Process/context.md) – See the nested context for details.
* [Properties](Properties/context.md) – See the nested context for details.
* [Props](Props/context.md) – See the nested context for details.
* [Router](Router/context.md) – See the nested context for details.
* [Supervision](Supervision/context.md) – See the nested context for details.
* [Timers](Timers/context.md) – See the nested context for details.
* [Utils](Utils/context.md) – See the nested context for details.

