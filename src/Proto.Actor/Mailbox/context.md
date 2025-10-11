# Mailbox Context

## Overview
Mailboxes and dispatchers responsible for scheduling actor message processing.

_Parent context: [Proto Actor](../context.md)_

## Key Files
* `BatchingMailbox.cs` – C# source defining Batching Mailbox behavior.
* `BoundedMailbox.cs` – C# source defining Bounded Mailbox behavior.
* `BoundedMailboxQueue.cs` – C# source defining Bounded Mailbox Queue behavior.
* `DefaultMailbox.cs` – C# source defining Default Mailbox behavior.
* `Dispatcher.cs` – C# source defining Dispatcher behavior.
* `IMailbox.cs` – C# source defining I Mailbox behavior.
* `LockingUnboundedMailboxQueue.cs` – C# source defining Locking Unbounded Mailbox Queue behavior.
* `MPMCQueue.cs` – C# source defining MPMC Queue behavior.
* `Messages.cs` – C# source defining Messages behavior.
* `NonBlockingBoundedMailbox.cs` – C# source defining Non Blocking Bounded Mailbox behavior.
* `Queue.cs` – C# source defining Queue behavior.
* `UnboundedMailbox.cs` – C# source defining Unbounded Mailbox behavior.
* `UnboundedMailboxQueue.cs` – C# source defining Unbounded Mailbox Queue behavior.

## Primary Types and Contracts
* `Record MessageBatch` defined in `BatchingMailbox.cs`
* `Class BatchingMailbox` defined in `BatchingMailbox.cs`
* `Class BoundedMailbox` defined in `BoundedMailbox.cs`
* `Class BoundedMailboxQueue` defined in `BoundedMailboxQueue.cs`
* `Class DefaultMailbox` defined in `DefaultMailbox.cs`
* `Interface IMailboxStatistics` defined in `DefaultMailbox.cs`
* `Interface IMessageInvoker` defined in `Dispatcher.cs`
* `Interface IDispatcher` defined in `Dispatcher.cs`
* `Class Dispatchers` defined in `Dispatcher.cs`
* `Class SynchronousDispatcher` defined in `Dispatcher.cs`
* `Class ThreadPoolDispatcher` defined in `Dispatcher.cs`
* `Class CurrentSynchronizationContextDispatcher` defined in `Dispatcher.cs`
* `Class NoopDispatcher` defined in `Dispatcher.cs`
* `Class NoopInvoker` defined in `Dispatcher.cs`
* `Interface IMailbox` defined in `IMailbox.cs`
* `Class LockingUnboundedMailboxQueue` defined in `LockingUnboundedMailboxQueue.cs`
* `Class MPMCQueue` defined in `MPMCQueue.cs`
* `Struct Cell` defined in `MPMCQueue.cs`
* `Interface SystemMessage` defined in `Messages.cs`
* `Class SuspendMailbox` defined in `Messages.cs`
* `Class ResumeMailbox` defined in `Messages.cs`
* `Class NonBlockingBoundedMailbox` defined in `NonBlockingBoundedMailbox.cs`
* `Interface IMailboxQueue` defined in `Queue.cs`
* `Class UnboundedMailbox` defined in `UnboundedMailbox.cs`
* `Class UnboundedMailboxQueue` defined in `UnboundedMailboxQueue.cs`

## Related Subcontexts
* No nested subcontexts.

