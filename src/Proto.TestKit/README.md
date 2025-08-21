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
