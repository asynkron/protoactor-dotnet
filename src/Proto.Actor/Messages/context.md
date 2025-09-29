# Messages Context

## Overview
Messages directory within the Proto.Actor repository.

_Parent context: [Proto Actor](../context.md)_

## Key Files
* `IAutoRespond.cs` – C# source defining I Auto Respond behavior.
* `IMessageBatch.cs` – C# source defining I Message Batch behavior.
* `MessageEnvelope.cs` – C# source defining Message Envelope behavior.
* `MessageExtensions.cs` – C# source defining Message Extensions behavior.
* `MessageHeader.cs` – C# source defining Message Header behavior.
* `Messages.cs` – C# source defining Messages behavior.

## Primary Types and Contracts
* `Interface IAutoRespond` defined in `IAutoRespond.cs`
* `Interface IMessageBatch` defined in `IMessageBatch.cs`
* `Record MessageEnvelope` defined in `MessageEnvelope.cs`
* `Class Terminated` defined in `MessageExtensions.cs`
* `Record MessageHeader` defined in `MessageHeader.cs`
* `Interface InfrastructureMessage` defined in `Messages.cs`
* `Interface IIgnoreDeadLetterLogging` defined in `Messages.cs`
* `Class Terminated` defined in `Messages.cs`
* `Class Restarting` defined in `Messages.cs`
* `Class Touch` defined in `Messages.cs`
* `Class PoisonPill` defined in `Messages.cs`
* `Class Failure` defined in `Messages.cs`
* `Class Watch` defined in `Messages.cs`
* `Class Unwatch` defined in `Messages.cs`
* `Class Restart` defined in `Messages.cs`
* `Class Stop` defined in `Messages.cs`
* `Class Stopping` defined in `Messages.cs`
* `Class Started` defined in `Messages.cs`
* `Class Stopped` defined in `Messages.cs`
* `Class ReceiveTimeout` defined in `Messages.cs`
* `Interface INotInfluenceReceiveTimeout` defined in `Messages.cs`
* `Class Continuation` defined in `Messages.cs`
* `Record ProcessDiagnosticsRequest` defined in `Messages.cs`
* `Class Nothing` defined in `Messages.cs`

## Related Subcontexts
* No nested subcontexts.

