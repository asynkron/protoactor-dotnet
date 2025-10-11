# Event Stream Context

## Overview
Event stream pub-sub infrastructure used internally by the runtime.

_Parent context: [Proto Actor](../context.md)_

## Key Files
* `DeadLetter.cs` – C# source defining Dead Letter behavior.
* `EventExpectation.cs` – C# source defining Event Expectation behavior.
* `EventProbe.cs` – C# source defining Event Probe behavior.
* `EventProbeLogMessages.cs` – C# source defining Event Probe Log Messages behavior.
* `EventStream.cs` – C# source defining Event Stream behavior.
* `EventStreamLogMessages.cs` – C# source defining Event Stream Log Messages behavior.
* `EventStreamProcess.cs` – C# source defining Event Stream Process behavior.

## Primary Types and Contracts
* `Class DeadLetterEvent` defined in `DeadLetter.cs`
* `Class DeadLetterProcess` defined in `DeadLetter.cs`
* `Class DeadLetterException` defined in `DeadLetter.cs`
* `Class EventExpectation` defined in `EventExpectation.cs`
* `Class EventStreamExtensions` defined in `EventProbe.cs`
* `Class EventProbe` defined in `EventProbe.cs`
* `Class EventProbeLogMessages` defined in `EventProbeLogMessages.cs`
* `Class EventStream` defined in `EventStream.cs`
* `Class EventStream` defined in `EventStream.cs`
* `Class EventStreamSubscription` defined in `EventStream.cs`
* `Class EventStreamLogMessages` defined in `EventStreamLogMessages.cs`
* `Class EventStreamProcess` defined in `EventStreamProcess.cs`

## Related Subcontexts
* No nested subcontexts.

