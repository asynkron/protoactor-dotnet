# Proto.TestKit

`Proto.TestKit` provides utilities for writing unit tests against Proto.Actor components.

## TestProbe: observing messages

`TestProbe` is an actor that records incoming messages asynchronously. Use it to verify
that actors send the expected messages or to assert that no messages are received
within a given period.

```csharp
var probe = new TestProbe();
system.Root.Spawn(Props.FromProducer(() => probe));
await probe.ExpectNextUserMessageAsync<string>();
await probe.ExpectNoMessageAsync(TimeSpan.FromMilliseconds(100));
```

Practical when: you need fine‑grained assertions about message flow without modifying
the actors under test.

## Mailbox statistics

`TestMailboxStats` captures mailbox activity such as posted and received messages. It helps
diagnose how an actor uses its mailbox and whether specific messages were enqueued.

```csharp
var stats = new TestMailboxStats(msg => msg is MyMessage);
var props = Props.FromProducer(() => new MyActor())
    .WithMailbox(() => UnboundedMailbox.Create(stats));
```

Practical when: you want insight into mailbox throughput or to confirm that particular
messages entered the mailbox.

## Props extensions

The TestKit includes `Props` extensions that instrument actors:

- `WithReceiveProbe` intercepts messages an actor receives.
- `WithSendProbe` observes messages an actor sends.
- `WithMailboxProbe` taps into mailbox traffic using `ProbeMailboxStatistics`.

These helpers are useful when debugging complex message interactions while keeping
the actor code unchanged.

## Awaiting Conditions

Use `AwaitConditionAsync` to poll for a condition until it becomes true:

```csharp
using static Proto.TestKit.TestKit;

await AwaitConditionAsync(() => value == expected, TimeSpan.FromSeconds(5), "value was never updated");
```

The helper evaluates the condition every 20ms and throws a `TimeoutException`—including
the optional message—if the condition is not met within the supplied timeout.

Practical when: a test depends on asynchronous state changes and busy‑waiting would
introduce flakiness.

## Deterministic Scheduler Tests

`Proto.Timers` exposes a test hook, `ISchedulerHook`, that signals when a `Scheduler`
has registered its internal `Task.Delay` timer. When testing with `FakeTimeProvider`,
await this hook before advancing time to avoid flakiness:

```csharp
var hook = new TestSchedulerHook();
var scheduler = ctx.Scheduler(fakeTimeProvider, hook);

scheduler.SendOnce(TimeSpan.FromSeconds(5), target, "Wakeup");
await hook.WaitAsync();
fakeTimeProvider.Advance(TimeSpan.FromSeconds(5));
```

Implement `ISchedulerHook` with a `TaskCompletionSource` to observe timer registration.
This allows tests to deterministically control when scheduled messages are delivered.

Practical when: verifying behavior that relies on timers or scheduled messages.

