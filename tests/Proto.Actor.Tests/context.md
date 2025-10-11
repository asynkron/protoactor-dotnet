# Proto Actor Tests Context

## Overview
Unit tests covering the Proto.Actor runtime behavior, mailboxes, supervision, futures, headers, and diagnostics.

_Parent context: [Tests](../context.md)_

## Key Files
* `ActorMetricsTests.cs` – C# source defining Actor Metrics Tests behavior.
* `ActorTestBase.cs` – C# source defining Actor Test Base behavior.
* `ActorTests.cs` – C# source defining Actor Tests behavior.
* `AssemblyInfo.cs` – C# source defining Assembly Info behavior.
* `BehaviorTests.cs` – C# source defining Behavior Tests behavior.
* `CaptureContextTests.cs` – C# source defining Capture Context Tests behavior.
* `DeadLetterResponseTests.cs` – C# source defining Dead Letter Response Tests behavior.
* `DeduplicationContextTests.cs` – C# source defining Deduplication Context Tests behavior.
* `DisposableActorTests.cs` – C# source defining Disposable Actor Tests behavior.
* `EventStreamTests.cs` – C# source defining Event Stream Tests behavior.
* `FunctionActorTests.cs` – C# source defining Function Actor Tests behavior.
* `GuardianProcessTests.cs` – C# source defining Guardian Process Tests behavior.
* `LoggingExtensionTests.cs` – C# source defining Logging Extension Tests behavior.
* `MiddlewareTests.cs` – C# source defining Middleware Tests behavior.
* `ObservableGaugeWrapperTests.cs` – C# source defining Observable Gauge Wrapper Tests behavior.
* `PIDTests.cs` – C# source defining PID Tests behavior.
* `PoisonTests.cs` – C# source defining Poison Tests behavior.
* `ProcessRegistryTests.cs` – C# source defining Process Registry Tests behavior.
* `PropsTests.cs` – C# source defining Props Tests behavior.
* `Proto.Actor.Tests.csproj` – Project file configuring compilation targets and dependencies.
* `ReceiveTimeoutTests.cs` – C# source defining Receive Timeout Tests behavior.
* `ReenterAfterExtensionsDecoratorTests.cs` – C# source defining Reenter After Extensions Decorator Tests behavior.
* `ReenterTests.cs` – C# source defining Reenter Tests behavior.
* `SchedulerTests.cs` – C# source defining Scheduler Tests behavior.
* `SpawnTests.cs` – C# source defining Spawn Tests behavior.
* `StashingTests.cs` – C# source defining Stashing Tests behavior.
* `StoreTests.cs` – C# source defining Store Tests behavior.
* `SupervisionTests_ActorSupervisorStrategy.cs` – C# source defining Supervision Tests Actor Supervisor Strategy behavior.
* `SupervisionTests_AllForOne.cs` – C# source defining Supervision Tests All For One behavior.
* `SupervisionTests_AlwaysRestart.cs` – C# source defining Supervision Tests Always Restart behavior.
* `SupervisionTests_ExponentialBackoff.cs` – C# source defining Supervision Tests Exponential Backoff behavior.
* `SupervisionTests_OneForOne.cs` – C# source defining Supervision Tests One For One behavior.
* `TestProbeAsyncTests.cs` – C# source defining Test Probe Async Tests behavior.
* `TimerExtensionsTests.cs` – C# source defining Timer Extensions Tests behavior.
* `UnknownSystemMessageTests.cs` – C# source defining Unknown System Message Tests behavior.
* `WatchTests.cs` – C# source defining Watch Tests behavior.

## Primary Types and Contracts
* `Class ActorMetricsTests` defined in `ActorMetricsTests.cs`
* `Class ActorTestBase` defined in `ActorTestBase.cs`
* `Class MyAutoRespondMessage` defined in `ActorTests.cs`
* `Class ActorTests` defined in `ActorTests.cs`
* `Class BehaviorTests` defined in `BehaviorTests.cs`
* `Class LightBulb` defined in `BehaviorTests.cs`
* `Class PressSwitch` defined in `BehaviorTests.cs`
* `Class Touch` defined in `BehaviorTests.cs`
* `Class HitWithHammer` defined in `BehaviorTests.cs`
* `Record Unstash` defined in `CaptureContextTests.cs`
* `Record UnstashResponse` defined in `CaptureContextTests.cs`
* `Record UnstashResult` defined in `CaptureContextTests.cs`
* `Class CaptureContextActor` defined in `CaptureContextTests.cs`
* `Class CaptureContextTests` defined in `CaptureContextTests.cs`
* `Class DeadLetterResponseTests` defined in `DeadLetterResponseTests.cs`
* `Class DeadLetterResponseValidationActor` defined in `DeadLetterResponseTests.cs`
* `Class DeduplicationContextTests` defined in `DeduplicationContextTests.cs`
* `Class DisposableActorTests` defined in `DisposableActorTests.cs`
* `Class SupervisingActor` defined in `DisposableActorTests.cs`
* `Class AsyncDisposableActor` defined in `DisposableActorTests.cs`
* `Class DisposableActor` defined in `DisposableActorTests.cs`
* `Class ParentWithMultipleChildrenActor` defined in `DisposableActorTests.cs`
* `Class EventStreamTests` defined in `EventStreamTests.cs`
* `Class FunctionActorTests` defined in `FunctionActorTests.cs`
* `Class GuardianProcessTests` defined in `GuardianProcessTests.cs`
* `Class NoopStrategy` defined in `GuardianProcessTests.cs`
* `Class RecordingProcess` defined in `GuardianProcessTests.cs`
* `Class LoggingExtensionTests` defined in `LoggingExtensionTests.cs`
* `Class TestContextDecorator` defined in `MiddlewareTests.cs`
* `Class MiddlewareTests` defined in `MiddlewareTests.cs`
* `Class ObservableGaugeWrapperTests` defined in `ObservableGaugeWrapperTests.cs`
* `Class PidTests` defined in `PIDTests.cs`
* `Class PoisonTests` defined in `PoisonTests.cs`
* `Class ProcessRegistryTests` defined in `ProcessRegistryTests.cs`
* `Class PropsTests` defined in `PropsTests.cs`
* `Class DummyActor` defined in `PropsTests.cs`
* `Class ActorWithSystem` defined in `PropsTests.cs`
* `Class ReceiveTimeoutTests` defined in `ReceiveTimeoutTests.cs`
* `Record IgnoreMe` defined in `ReceiveTimeoutTests.cs`
* `Class ReenterAfterExtensionsDecoratorTests` defined in `ReenterAfterExtensionsDecoratorTests.cs`
* `Class CountingReenterDecorator` defined in `ReenterAfterExtensionsDecoratorTests.cs`
* `Class ReenterTests` defined in `ReenterTests.cs`
* `Class ReenterAfterCancellationActor` defined in `ReenterTests.cs`
* `Record Request` defined in `ReenterTests.cs`
* `Record Response` defined in `ReenterTests.cs`
* `Class SchedulerTests` defined in `SchedulerTests.cs`
* `Class TestSchedulerHook` defined in `SchedulerTests.cs`
* `Class SpawnTests` defined in `SpawnTests.cs`
* `Class StashingActor` defined in `StashingTests.cs`
* `Class ApplyActor` defined in `StashingTests.cs`
* `Class StashingTests` defined in `StashingTests.cs`
* `Class StoreTests` defined in `StoreTests.cs`
* `Class StoreType` defined in `StoreTests.cs`
* `Class SupervisionTestsActorSupervisorStrategy` defined in `SupervisionTests_ActorSupervisorStrategy.cs`
* `Class SupervisingActor` defined in `SupervisionTests_ActorSupervisorStrategy.cs`
* `Class NonSupervisingParent` defined in `SupervisionTests_ActorSupervisorStrategy.cs`
* `Class FailingChildActor` defined in `SupervisionTests_ActorSupervisorStrategy.cs`
* `Class TrackingSupervisorStrategy` defined in `SupervisionTests_ActorSupervisorStrategy.cs`
* `Class SupervisionTestsAllForOne` defined in `SupervisionTests_AllForOne.cs`
* `Class ParentActor` defined in `SupervisionTests_AllForOne.cs`
* `Class ChildActor` defined in `SupervisionTests_AllForOne.cs`
* `Class SupervisionTestsAlwaysRestart` defined in `SupervisionTests_AlwaysRestart.cs`
* `Class ParentActor` defined in `SupervisionTests_AlwaysRestart.cs`
* `Class ChildActor` defined in `SupervisionTests_AlwaysRestart.cs`
* `Class SupervisionTestsExponentialBackoff` defined in `SupervisionTests_ExponentialBackoff.cs`
* `Class ParentActor` defined in `SupervisionTests_ExponentialBackoff.cs`
* `Class BackoffChild` defined in `SupervisionTests_ExponentialBackoff.cs`
* `Class SupervisionTestsOneForOne` defined in `SupervisionTests_OneForOne.cs`
* `Class ParentActor` defined in `SupervisionTests_OneForOne.cs`
* `Class ChildActor` defined in `SupervisionTests_OneForOne.cs`
* `Class ThrowOnStartedChildActor` defined in `SupervisionTests_OneForOne.cs`
* `Class TestProbeAsyncTests` defined in `TestProbeAsyncTests.cs`
* `Class TimerExtensionsTests` defined in `TimerExtensionsTests.cs`
* `Class TestSchedulerHook` defined in `TimerExtensionsTests.cs`
* `Class UnknownSystemMessageTests` defined in `UnknownSystemMessageTests.cs`
* `Class UnknownSystemMessage` defined in `UnknownSystemMessageTests.cs`
* `Record GetChild` defined in `UnknownSystemMessageTests.cs`
* `Class ParentActor` defined in `UnknownSystemMessageTests.cs`
* `Class WatchTests` defined in `WatchTests.cs`

## Related Subcontexts
* [Dependency Injection](DependencyInjection/context.md) – See the nested context for details.
* [Diagnostics](Diagnostics/context.md) – See the nested context for details.
* [Extensions](Extensions/context.md) – See the nested context for details.
* [Futures](Futures/context.md) – See the nested context for details.
* [Headers](Headers/context.md) – See the nested context for details.
* [Mailbox](Mailbox/context.md) – See the nested context for details.
* [Router](Router/context.md) – See the nested context for details.
* [Utils](Utils/context.md) – See the nested context for details.

