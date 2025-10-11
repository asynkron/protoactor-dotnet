# Proto.TestKit

`Proto.TestKit` provides helpers for unit testing Proto.Actor systems. Each feature is
exposed as a dedicated class with optional extensions.

## TestProbe

`TestProbe` is an actor that records messages and exposes methods to inspect or assert
on them. It implements `ITestProbe`.

### Creating a probe

Use the `ActorSystem` or `RootContext` extensions to create and spawn a probe in one
step:

```csharp
var root = system.Root;
var (probe, pid) = root.CreateTestProbe();
root.Send(pid, "ping");
await probe.ExpectNextUserMessageAsync<string>();
await probe.ExpectNoMessageAsync(TimeSpan.FromMilliseconds(100));
```

### Attaching probes to actors

`Props` extensions let a probe observe actor communication:

```csharp
var (probe, _) = system.CreateTestProbe();
var props = Props.FromProducer(() => new MyActor())
    .WithReceiveProbe(probe)   // messages after processing
    .WithSendProbe(probe)      // messages sent by the actor
    .WithMailboxProbe(probe);  // mailbox traffic via ProbeMailboxStatistics

system.Root.Spawn(props);
```

These helpers allow inspection of message flow without modifying actor code.

## TestMailboxStats

`TestMailboxStats` implements `IMailboxStatistics` and collects posted/received
messages. Configure it via `WithTestMailboxStats`:

```csharp
var stats = new TestMailboxStats(m => m is MyMessage);
var props = Props.FromProducer(() => new MyActor())
    .WithTestMailboxStats(stats);
var pid = system.Root.Spawn(props);
await stats.WaitForResetAsync(TimeSpan.FromSeconds(1));
```

Use it to verify that specific messages enter or leave the mailbox.

## ProbeMailboxStatistics

`ProbeMailboxStatistics` forwards each processed mailbox message to a `TestProbe`. It
is used by `Props.WithMailboxProbe`:

```csharp
var (probe, _) = system.CreateTestProbe();
var props = Props.FromProducer(() => new MyActor())
    .WithMailboxProbe(probe);
```

## TestKit

`TestKit` exposes utilities such as `AwaitConditionAsync` for polling conditions:

```csharp
using static Proto.TestKit.TestKit;

await AwaitConditionAsync(() => state.Ready, TimeSpan.FromSeconds(5),
    "state not ready");
```

## TestKitException

`TestKitException` is thrown when probe expectations fail. Catch it to obtain detailed
error information during tests.

## ISchedulerHook

`ISchedulerHook` (defined in `Proto.Actor`) can be implemented by tests to observe when
a `Scheduler` registers internal timers. This enables deterministic timer tests, for
example with a fake time provider:

```csharp
var hook = new TestSchedulerHook();
var scheduler = ctx.Scheduler(fakeTimeProvider, hook);

scheduler.SendOnce(TimeSpan.FromSeconds(5), target, "Wakeup");
await hook.WaitAsync();
fakeTimeProvider.Advance(TimeSpan.FromSeconds(5));
```

Implement the hook with a `TaskCompletionSource` to await timer registration before
advancing time.

