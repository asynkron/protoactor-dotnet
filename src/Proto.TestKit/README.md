# Proto.TestKit

`Proto.TestKit` provides utilities for writing unit tests against Proto.Actor components.

## Awaiting Conditions

Use `AwaitConditionAsync` to poll for a condition until it becomes true:

```csharp
using static Proto.TestKit.TestKit;

await AwaitConditionAsync(() => value == expected, TimeSpan.FromSeconds(5), "value was never updated");
```

The helper evaluates the condition every 20ms and throws a `TimeoutException`—including the optional
message—if the condition is not met within the supplied timeout.

## Deterministic Scheduler Tests

`Proto.Timers` exposes a test hook, `ISchedulerHook`, that signals when a
`Scheduler` has registered its internal `Task.Delay` timer. When testing with
`FakeTimeProvider`, await this hook before advancing time to avoid flakiness:

```csharp
var hook = new TestSchedulerHook();
var scheduler = ctx.Scheduler(fakeTimeProvider, hook);

scheduler.SendOnce(TimeSpan.FromSeconds(5), target, "Wakeup");
await hook.WaitAsync();
fakeTimeProvider.Advance(TimeSpan.FromSeconds(5));
```

Implement `ISchedulerHook` with a `TaskCompletionSource` to observe timer
registration. This allows tests to deterministically control when scheduled
messages are delivered.
