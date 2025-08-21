# Actor TestKit Example

This example shows how to test actors using [`Proto.TestKit`](https://github.com/asynkron/protoactor-dotnet).

`TestKitFixture` sets up an `ActorSystem` and a default `TestProbe`. Each call to `SetUp` creates a new
system, so tests run in full isolation. A probe is an actor that stores incoming messages, allowing
tests to inspect them later.

```csharp
var pid = _fixture.Spawn<EchoActor>();
_fixture.Request(pid, new Ping("Proto"));
var reply = _fixture.GetNextMessage<Pong>();
```

`GetNextMessage<T>` dequeues the next message of the specified type. If the type does not match or no
message arrives within the default timeout, a `TestKitException` is thrown. `ExpectNoMessage` asserts
that the probe did not receive anything during the interval.

Additional probes can be created when isolation between tests is needed:

```csharp
var probe = _fixture.CreateTestProbe();
_fixture.Context.Send(probe, "hello");
probe.GetNextMessage<string>();
```

Probes capture messages in FIFO order, so assertions reflect the sequence of events in the actor under test.

## Customizing the ActorSystem

`TestKitBase` can create an `ActorSystem` for you, but you can also supply your own system or
configuration to tweak settings per test. For example:

```csharp
public class CustomConfigTests : TestKitBase
{
    public CustomConfigTests()
        : base(new ActorSystemConfig().WithDeveloperSupervisionLogging(true))
    {
        SetUp();
    }
}
```

Passing an `ActorSystemConfig` or a pre-built `ActorSystem` allows individual tests to enable
features like developer supervision logging or metrics without affecting other tests.

