# Future Context

## Overview
Future/TCS helpers for awaiting actor responses. Shared futures now clamp their request id wrap-around into the inclusive
range `[1, max]` so recycled slots never yield the sentinel `0` value.

_Parent context: [Proto Actor](../context.md)_

## Key Files
* `FutureBatch.cs` – C# source defining Future Batch behavior.
* `Futures.cs` – C# source defining Futures behavior.
* `SharedFuture.cs` – C# source defining Shared Future behavior.

## Primary Types and Contracts
* `Class FutureBatchProcess` defined in `FutureBatch.cs`
* `Class SimpleFutureHandle` defined in `FutureBatch.cs`
* `Interface IFuture` defined in `Futures.cs`
* `Class FutureFactory` defined in `Futures.cs`
* `Class FutureProcess` defined in `Futures.cs`
* `Class SharedFutureProcess` defined in `SharedFuture.cs`
* `Class SharedFutureHandle` defined in `SharedFuture.cs`
* `Class FutureHandle` defined in `SharedFuture.cs`

## Related Subcontexts
* No nested subcontexts.

