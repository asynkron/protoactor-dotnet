# Context Context

## Overview
ActorContext and IContext implementations orchestrating message flow, lifecycle, and middleware.

_Parent context: [Proto Actor](../context.md)_

## Key Files
* `ActorContext.cs` – C# source defining Actor Context behavior.
* `ActorContextDecorator.cs` – C# source defining Actor Context Decorator behavior.
* `ActorContextExtras.cs` – C# source defining Actor Context Extras behavior.
* `ActorContextLogMessages.cs` – C# source defining Actor Context Log Messages behavior.
* `ActorLoggingContext.cs` – C# source defining Actor Logging Context behavior.
* `ActorLoggingContextExtensions.cs` – C# source defining Actor Logging Context Extensions behavior.
* `BatchContext.cs` – C# source defining Batch Context behavior.
* `BatchContextLogMessages.cs` – C# source defining Batch Context Log Messages behavior.
* `CapturedContext.cs` – C# source defining Captured Context behavior.
* `ContextState.cs` – C# source defining Context State behavior.
* `DeadlineContextDecorator.cs` – C# source defining Deadline Context Decorator behavior.
* `DeadlineContextDecoratorLogMessages.cs` – C# source defining Deadline Context Decorator Log Messages behavior.
* `IContext.cs` – C# source defining I Context behavior.
* `IContextStore.cs` – C# source defining I Context Store behavior.
* `IInfoContext.cs` – C# source defining I Info Context behavior.
* `IReceiverContext.cs` – C# source defining I Receiver Context behavior.
* `ISenderContext.cs` – C# source defining I Sender Context behavior.
* `ISpawnerContext.cs` – C# source defining I Spawner Context behavior.
* `IStopperContext.cs` – C# source defining I Stopper Context behavior.
* `ISystemContext.cs` – C# source defining I System Context behavior.
* `PassivationContextDecorator.cs` – C# source defining Passivation Context Decorator behavior.
* `ReenterAfterExtensions.cs` – C# source defining Reenter After Extensions behavior.
* `RootContext.cs` – C# source defining Root Context behavior.
* `RootContextDecorator.cs` – C# source defining Root Context Decorator behavior.
* `RootContextLogMessages.cs` – C# source defining Root Context Log Messages behavior.
* `RootLoggingContext.cs` – C# source defining Root Logging Context behavior.
* `SenderContextLogMessages.cs` – C# source defining Sender Context Log Messages behavior.
* `StartupDeadlineContextDecorator.cs` – C# source defining Startup Deadline Context Decorator behavior.
* `StartupDeadlineContextDecoratorLogMessages.cs` – C# source defining Startup Deadline Context Decorator Log Messages behavior.
* `SystemContext.cs` – C# source defining System Context behavior.
* `SystemContextLogMessages.cs` – C# source defining System Context Log Messages behavior.

## Primary Types and Contracts
* `Class ActorContext` defined in `ActorContext.cs`
* `Class ActorContextDecorator` defined in `ActorContextDecorator.cs`
* `Class ActorContextExtras` defined in `ActorContextExtras.cs`
* `Class ActorContextLogMessages` defined in `ActorContextLogMessages.cs`
* `Class ActorLoggingContext` defined in `ActorLoggingContext.cs`
* `Class ActorLoggingContextExtensions` defined in `ActorLoggingContextExtensions.cs`
* `Class BatchContext` defined in `BatchContext.cs`
* `Class BatchContextLogMessages` defined in `BatchContextLogMessages.cs`
* `Record CapturedContext` defined in `CapturedContext.cs`
* `Enum ContextState` defined in `ContextState.cs`
* `Class DeadlineContextExtensions` defined in `DeadlineContextDecorator.cs`
* `Class DeadlineContextDecorator` defined in `DeadlineContextDecorator.cs`
* `Class DeadlineContextDecoratorLogMessages` defined in `DeadlineContextDecoratorLogMessages.cs`
* `Interface IContext` defined in `IContext.cs`
* `Interface IContextStore` defined in `IContextStore.cs`
* `Interface IInfoContext` defined in `IInfoContext.cs`
* `Interface IReceiverContext` defined in `IReceiverContext.cs`
* `Interface ISenderContext` defined in `ISenderContext.cs`
* `Class SenderContextExtensions` defined in `ISenderContext.cs`
* `Interface ISpawnerContext` defined in `ISpawnerContext.cs`
* `Class SpawnerContextExtensions` defined in `ISpawnerContext.cs`
* `Interface IStopperContext` defined in `IStopperContext.cs`
* `Interface ISystemContext` defined in `ISystemContext.cs`
* `Class PassivationContextExtensions` defined in `PassivationContextDecorator.cs`
* `Class PassivationContextDecorator` defined in `PassivationContextDecorator.cs`
* `Class ReenterAfterExtensions` defined in `ReenterAfterExtensions.cs`
* `Interface IRootContext` defined in `RootContext.cs`
* `Record RootContext` defined in `RootContext.cs`
* `Class RootContextDecorator` defined in `RootContextDecorator.cs`
* `Class RootContextLogMessages` defined in `RootContextLogMessages.cs`
* `Class RootLoggingContext` defined in `RootLoggingContext.cs`
* `Class SenderContextLogMessages` defined in `SenderContextLogMessages.cs`
* `Class StartupDeadlineContextExtensions` defined in `StartupDeadlineContextDecorator.cs`
* `Class StartupDeadlineContextDecorator` defined in `StartupDeadlineContextDecorator.cs`
* `Class StartupDeadlineContextDecoratorLogMessages` defined in `StartupDeadlineContextDecoratorLogMessages.cs`
* `Class SystemContext` defined in `SystemContext.cs`
* `Class SystemContextLogMessages` defined in `SystemContextLogMessages.cs`

## Related Subcontexts
* No nested subcontexts.

