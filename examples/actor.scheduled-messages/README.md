# ScheduledMessages

Demonstrates how to schedule future messages using `Scheduler.SendOnce` and `Scheduler.SendRepeatedly`.

The scheduler lets an actor or external context send messages after a delay or at regular intervals. Cancellation tokens returned from the scheduler must be cancelled to avoid the messages continuing after they are no longer needed.

`ReceiveTimeout` is a specialized form of scheduling built into actors. While scheduled messages fire regardless of other activity, `ReceiveTimeout` sends a `ReceiveTimeout` message only after a period of inactivity and is reset whenever the actor receives a normal message (unless it implements `INotInfluenceReceiveTimeout`).
