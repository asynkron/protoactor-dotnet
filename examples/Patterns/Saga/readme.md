# Money Transfer Saga

- [Part 1 - The Scenario](#1)
- [Part 2 - The Implementation](#2)
- [Part 3 - The Audit Log](#3)
- [Part 4 - Supervision, error kernels and idempotency](#4)
- [Part 5 - Results](#5)

The Saga pattern was first coined by Hector Garcia-Molina and Kenneth Salem in their
paper, [Sagas](http://www.cs.cornell.edu/andru/cs711/2002fa/reading/sagas.pdf). Although originally described in the
context of a database management system, the Saga pattern has gained popularity in a distributed systems context as a
way to manage failures when dealing with multiple remote participants in a business process. The paper describes a saga
as

> a long-lived transaction that can be written as a sequence of transactions that can be interleaved with other
> transactions

It also says that
> the system guarentees that either all the transactions in a saga are successfully completed or compensatmg
> transactions are run to amend a partial execution

For each step in a business process, given a failure, there should be a compensating step that rolls-back the state of
the system to what it would have been before the step was applied. The whole process is managed by something called a
Saga Execution Component. A good introduction to the Saga pattern
is [Distributed Sagas: A Protocol for Coordinating Microservices](https://www.youtube.com/watch?v=0UTOLRTwOX0&t=1696s)
by Caitie McCaffrey,

# Part 1 - The Scenario <a name="1"></a>

The scenario here is a simple bank account transfer - we want to transfer €10
from Account1 to Account2. Each account has a starting balance of €10. If successful, Account1 will end with a balance
of €0 and Account2 will have a balance of €20. Our aim is to have a transaction-like result for the account transfer
process where even if one of the steps fails, the system is left in a consistent state (i.e. either the whole process
fails, resulting in €10 in each account, or the process succeeds)

On the face of it this is a simple problem. However, things get more interesting when you consider each account as a
remote service, and thus subject to the usual issues associated with remote calls. The accounts can misbehave in a
number of ways:

* Refuse to process a request (for example, if an account has been suspended or closed for some reason)
* Crash before adjusting the balance of the account
* Crash _after_ adjusting the balance of the account
* Return a "i'm busy" response
* Be slow

For our example, we have only a single area where a compensating action might need to be applied - if we are able to
debit from Account1 but unable to credit Account2, the debit should be rolled back (a compensating action of crediting
Account1 should be applied). Otherwise, Account1 will be €0 and Account2 will be €10, and €10 has been lost in the
system.

There are many workflows for what seems a simple use case:

1. Account1 processes successfully -> CREDIT ACCOUNT2
2. Account1 refuses the debit request. -> STOP
3. Account1 responds with "i'm busy" -> RETRY
4. Account1 does not respond -> RETRY

If Account1 responds successfully, then we have the following possibilities

5. Account2 processes successfully -> SUCCESS
6. Account2 refuses the credit request -> ROLLBACK DEBIT
7. Account2 responds with "i'm busy" -> RETRY
8. Account2 does not respond -> RETRY

If we have to rollback the debit:

8. Account1 processes successfully -> STOP
9. Account1 refuses the credit request -> ESCALATE
10. Account1 responds with "i'm busy" -> RETRY
11. Account1 does not respond -> RETRY

and finally:

12. the TransferProcess saga itself crashes -> RESUME

Each of these possibilities requires handling. In situations where there is either a "i'm busy" response, or no response
at all, we should retry the request. If the request is outright refused, there is no point in retrying, so we should
stop or rollback the saga.

One situation that presents a problem is when we receive no reply to our requests, even after retrying. In our example
bank transfer process, what do we know has happened in this scenario? - can we say for sure that an account did not
process the message? Maybe the account was credited/debited but the service failed to respond? If the first step of
debiting an account is successful, but then the second step of crediting an account does not respond, do we rollback the
debit of the first account? We know it was successful but what we don't know is if the money made it to the other
account.

What if a compensating action fails?

### Escalation

In the preceding section we descovered scenarios where we are not sure what state the system is in. Even with retries
and compensating actions, things can still go wrong. In an ideal world, these should be very rare! However, they can
occur and in these cases it's best to have a fallback strategy, escalating the result of the saga to something else,
quite possibly a manual / human process.

### Atomicitiy

One thing a saga does not provide is atomicitiy. In the bank account example above there's nothing to stop other systems
interacting with the accounts in-between the debit and the credit operations. This needs bearing in mind as it could
rule out the saga pattern for some types of operations.

  ___

# Part 2 - Implementing the Money Transfer Saga  <a name="2"></a>

The implementation of the transfer process saga contains the following actors:

* Account actor - this is a simulation of a troublesome remote service
* TransferProcess actor - this is the main actor that coordinates the process of debiting and crediting accounts. From a
  saga perspective, this is the Saga Execution Component that manages the transfer process.
* AccountProxy actor - this has the sole purpose of attempting to communicate with Account actor

## Account actor

The Account actor simulates a remote bank account service:

  ```c#
public Task ReceiveAsync(IContext context)
{
    switch (context.Message)
    {
        // ...
        case Credit msg:
            return AdjustBalance(msg.ReplyTo, msg.Amount);
        case Debit msg when msg.Amount + _balance >= 0:
            return AdjustBalance(msg.ReplyTo, msg.Amount);
        // ...
    }
    return Actor.Done;
}
```

When a `Credit` or `Debit` request is received, we attempt to adjust the balance of the account. The attempt may fail
for a number of reasons:

```c#
private Task AdjustBalance(PID replyTo, decimal amount)
{
    if (RefusePermanently())
    {
        _processedMessages.Add(replyTo, new Refused());
        replyTo.Tell(new Refused());
    }
        
    if (Busy())
        replyTo.Tell(new ServiceUnavailable());
    
    var behaviour = DetermineProcessingBehavior();
    if (behaviour == Behavior.FailBeforeProcessing)
        return Failure(replyTo);
    
    // simulate potential slow service
    Thread.Sleep(_random.Next(0, 150));
    
    _balance += amount;
    _processedMessages.Add(replyTo, new OK());
    
    if (behaviour == Behavior.FailAfterProcessing)
        return Failure(replyTo);
    
    replyTo.Tell(new OK());
    return Actor.Done;
}
```

This allows us to introduce a degree of randomness to the saga to simulate various types of failures.

## Account Proxy

The `AccountProxy` is created by the `TransferProcess` actor for the sole purpose of communicating with the `Account`
actor and handling the responses:

  ```c#
 class AccountProxy : IActor
{
    private readonly PID _account;
    private readonly Func<PID, object> _createMessage;

    public AccountProxy(PID account, Func<PID, object> createMessage)
    {
        _account = account;
        _createMessage = createMessage;
    }
    
    public Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case Started _:
                _account.Tell(_createMessage(context.Self));
                context.SetReceiveTimeout(TimeSpan.FromMilliseconds(100));
                break;
            case OK msg:
                context.CancelReceiveTimeout();
                context.Parent.Tell(msg);
                break;
            case Refused msg:
                context.CancelReceiveTimeout();
                context.Parent.Tell(msg);
                break;
            // These represent a failed remote call
            case InternalServerError _:
            case ReceiveTimeout _:
            case ServiceUnavailable _:
                throw new Exception();
        }
        
        return Actor.Done;
    }
}
```

The `AccountProxy` sends the message to the `Account` on `Started` and sets a timeout of 100 milliseconds in which a
response should be received. This allows us to simulate a slow remote process and handle timeouts as errors. Given any
message representing a failed remote call, the `AccountProxy` simply throws an exception, passing responsibility to the
parent to decide what to do. If the message is `OK` or `Refused` we cancel the timeout and pass the message to the
parent, the `TransferProcess` actor.

## Transfer Process actor

The `TransferProcess` actor has the responsibility of managing our transfer. In saga terminology, it is the Saga
Execution Component. This class keeps track of the steps that have been completed and applies any compensating actions
on failures.

The implementation of the `TransferProcess` actor utilises:

* Behavior plug-in
* Persistence plug-in
* Error kernel pattern
* Supervision

  ___

### Using Behaviors

Our Transfer Process is modelled as a state machine using the Behavior plugin. This allows us to swap out the message
handling code depending on our current state. This is achieved by delegating to the Behavior class when handling
messages:

  ```c#
 public async Task ReceiveAsync(IContext context)
{
    await _behavior.ReceiveAsync(context);
}
```

Here the `TransferProcess`'s `ReceiveAsync` method just delegates to the Behavior's `ReceiveAsync` method.

The possible TransferProcess states are

* Starting
* AwaitingDebitConfirmation
* AwaitingCreditConfirmation
* RollingBackDebit

Lets have a look at each state and see what changes.

#### Starting

Starting is the initial state of the actor and where we make the debit attempt.

```c#
private async Task Starting(IContext context)
{
    if (context.Message is Started)
    {
        context.SpawnNamed(TryDebit(_from, -_amount), "DebitAttempt");
        _behavior.Become(AwaitingDebitConfirmation);
    }
}

private Props TryDebit(PID targetActor, decimal amount) => Actor
            .FromProducer(() => new AccountProxy(targetActor, sender => new Debit(amount, sender)));
```

Here we create an actor specifically to handle the debit attempt and transition the actor to
the `AwaitingDebitConfirmation` state.

#### Awaiting Debit Confirmation

In this state there are 3 possible transitions:

* AwaitingDebitConfirmation -> AwaitingCreditConfirmation
* AwaitingDebitConfirmation -> Stop (ConsistentSystem)
* AwaitingDebitConfirmation -> Stop (Unknown)

```c#
private Task AwaitingDebitConfirmation(IContext context)
{
    switch (context.Message)
    {
        case OK _:
            _behavior.Become(AwaitingCreditConfirmation);
            context.SpawnNamed(TryCredit(_to, +_amount), "CreditAttempt");
            break;
        case Refused _:
            _logger.Log("Transfer failed. System consistent")
            StopAll(context);
            break;
        case Terminated _: 
            _logger.Log("Transfer status unknown. Escalate")
            StopAll(context);
            break;
    }
}

private Props TryCredit(PID targetActor, decimal amount) => Actor
            .FromProducer(() => new AccountProxy(targetActor, sender => new Credit(amount, sender)));   
```

Our 3 possible results from our `DebitAttempt` actor are:

##### 1. Debit Success

```c#
case OK _:
    _behavior.Become(AwaitingCreditConfirmation);
    context.SpawnNamed(TryCredit(_to, +_amount), "CreditAttempt");
```

The debit from the first step was successful, we can now create the `CreditAttempt` actor and transition
to `AwaitingCreditConfirmation` state.

##### 2. Debit Refused

```c#
case Refused _: 
    _logger.Log("Transfer failed. System consistent")
    StopAll(context);
```

The debit was refused and should not be retried. We log and stop.

##### 3. Unknown

```c#
case Terminated _: 
    _logger.Log("Transfer status unknown. Escalate")
    StopAll(context);
```

Receiving a `Terminated` message in the `AwaitingDebitConfirmation` state means the `DebitActor` has been stopped, and
represents an unknown failure of the debit process. The section on Supervision and error kernels describes how
this `Terminated` message is created, but it is enough for now to know that the debit has been retried but ultimately
failed. We have no option left but to stop.

#### Awaiting Credit Confirmation

Given a successful debit we transition to the `AwaitingCreditConfirmation` state. In this state there are 3 possible
transitions:

* AwaitingCreditConfirmation -> Stop (Success)
* AwaitingCreditConfirmation -> RollingBackDebit
* AwaitingCreditConfirmation -> Stop (Unknown)

```c#
private async Task AwaitingCreditConfirmation(IContext context)
{
    switch (context.Message)
    {
        //...
        case OK msg:
            _logger.Log("Success!")
            StopAll(context);
            break;
        case Refused msg:
            _behavior.Become(RollingBackDebit)
            context.SpawnNamed(TryCredit(_from, +_amount), "RollbackDebit");
            break;
        case Terminated msg:
            _logger.Log("Transfer status unknown. Escalate")
            StopAll(context);
            break;
    }
}
        
```

##### 1. Credit Success

```c#
case OK msg:
    _logger.Log("Success!")
    StopAll(context);
```

This represents the happy path - we have successfully completed the transfer process. Log the fact that we have
completed and stop.

##### 2. Credit Refused

```c#
case Refused msg:
    _behavior.Become(RollingBackDebit)
    context.SpawnNamed(TryCredit(_from, +_amount), "RollbackDebit");
```

The credit attempt has been refused. We know that the debit succeeded but the credit was refused, so we can attempt to
apply the compensating action of rolling back the debit by crediting the account. We create a `RollbackDebit` actor and
transition to the `RollingBackDebit` state.

##### 3. Unknown

```c#
case Terminated msg:
     _logger.Log("Transfer status unknown. Escalate")
    StopAll(context);
```

Receiving a `Terminated` message in the `AwaitingCreditConfirmation` state means the `CreditActor` has been stopped, and
represents an unknown failure of the credit process. We are now in a state where we know that the debit succeeded but do
not know the state of the credit. It _might_ have succeeded. I originally transitioned to the `RollingBackDebit` state
here but ultimately decided it is better to trigger whatever escalation strategy is in place to look at transfers in
this state.

#### Rolling Back Debit

If our debit was successful but our credit was refused, we transition to the `RollingBackDebit` state, where there are 2
possible transitions:

* RollingBackDebit -> Stop (ConsistentSystem)
* RollingBackDebit -> Stop (Unknown)

```c#
private async Task RollingBackDebit(IContext context)
{
    switch (context.Message)
    {
        //...
        case OK _:
            _logger.Log("Transfer failed. System consistent")
            StopAll(context);
            break;
        case Refused _: 
        case Terminated _:
            _logger.Log("Transfer status unknown. Escalate")
            StopAll(context);
            break;
    }
}
```

In this state there are only 2 possible transitions:

##### 1. Rollback Success

```c#
case OK _:
    _logger.Log("Transfer failed. System consistent")
    StopAll(context);
```

We have successfully applied the compensating action, and even though the actual transfer process has failed the system
is in a consistent state.

##### 2. Failure

```c#
case Refused _: 
case Terminated _:
    _logger.Log("Transfer status unknown. Escalate")
    StopAll(context);
```

Failure can be the result of either the account refusing the operation or the `RollbackDebit` actor terminating. If the
operation is refused, it means in between making the original debit and attempting the rollback credit the account has
decided to refuse the request.

If the `RollbackDebit` actor was terminated, it again means we do not know what happened to the operation, and again
have no option but to give up and rely on escalation.

We've now covered all of our possible state transitions through the use of the behavior plug-in. However, what if
the `TransferProcess` itself crashes? In Part 3 we'll look at how to handle this situation through the use of the
Persistence plugin to save the TransferProcess state.

___

## Part 3 - The Audit Log <a name="3"></a>

The orignal Saga paper discusses the use of a log to track the activities of the saga. The `TransferProcess` actor we
created in Part 2 covered all the state transitions for our saga, but did not save it's state so is vulnerable to
crashing. We can fix this by using the Persistence plugin.

The Persistence plug-in allows an actor to recover it's state when it is restarted or reloaded through the use of event
sourcing. Each state change is modelled as an event that is "applied" to the actor _both during the recovery phase and
when running live_. The Persistence plugin takes an `Action<Event> applyEvent` method as a parameter - this is called
whenever an event is saved, or loaded from the underlying storage during recovery. It is important that all state
changes are defined in this `ApplyEvent` method, _including transitioning to different behaviors_. In our
current `TransferProcess` code, we transition to a new behavior upon processing a message by
calling `_behavior.Become(SomeState)`. However, in order for the actor to be able to recover from failure and end up in
the correct state, all state changes need to move to the `ApplyEvent` method, and we need to save events as checkpoints.
So, for example, our Starting code changes from

 ```c#
 private Task Starting(IContext context)
 {
     if (context.Message is Started)
     {
         context.SpawnNamed(TryDebit(_from, -_amount), "DebitAttempt");
         _behavior.Become(AwaitingDebitConfirmation);
     }
 }
 ```

to

 ```c#
 private async Task Starting(IContext context)
 {
     if (context.Message is Started)
     {
         context.SpawnNamed(TryDebit(_from, -_amount), "DebitAttempt");
         await _persistence.PersistEventAsync(new TransferStarted());
     }
 }
 ```

with the corresponding behavior change added to the `ApplyEvent` method:

  ```c#
private void ApplyEvent(Event @event)
{
    switch (@event.Data)
    {
        case TransferStarted msg:
            _behavior.Become(AwaitingDebitConfirmation);
            break;
        // ... other transitions
    }
}
```

A side-effect of moving all state transitions into the `ApplyEvent` method is that it now reads like the state machine
we described in Part 1:

```c#
private void ApplyEvent(Event @event)
{
    switch (@event.Data)
    {
        case TransferStarted msg:
            _behavior.Become(AwaitingDebitConfirmation);
            break;
        case AccountDebited msg:
            _behavior.Become(AwaitingCreditConfirmation);
            break;
        case CreditRefused msg:
            _behavior.Become(RollingBackDebit);
            break;
        case AccountCredited _:
        case DebitRolledBack _:
        case TransferFailed _:
            _processCompleted = true;
            break;
    }
}
```

Having a log of all state change events is what allows us to recover from failures. The `TransferProcess` itself is
supervised, so when a failure occurs it is restarted by it's parent. This results in a Started message where we can
rebuild the state of the `TransferProcess`. We do this in the `TransferProcess` `ReceiveAsync` method before delegating
to the Behavior class:

 ```c#
 public async Task ReceiveAsync(IContext context)
{
    switch (context.Message)
    {
        case Started msg:
            _behavior.Become(Starting);
            await _persistence.RecoverStateAsync();
            break;
        // ... 
    }
    await _behavior.ReceiveAsync(context);
}
```

`RecoverStateAsync` will load all events from our storage and call the ApplyEvent method on them, thus rebuilding our
behavior state transitions, and putting out actor back into the correct state. Note the default to the `Starting` state

- if we do not have any events we need to begin in the `Starting` state. If we do have events, then the state will
  transition as normal.

### The worried parent - where did our children go?

In our current design, the actors that attempt the credit and debit operations are created when we receive messages from
the previous states. For example, when in the `AwaitingDebitConfirmation` state, if we receive a successful response we
spawn a new actor to attempt the credit:

```c#
private Task AwaitingDebitConfirmation(IContext context)
{
    switch (context.Message)
    {
        case OK _:
            await _persistence.PersistEventAsync(new AccountDebited());
            context.SpawnNamed(TryCredit(_to, +_amount), "CreditAttempt");
            break;
        //...
    }
}
```

There is a problem with this though - consider the scenario where the `OK` message is received, the `TransferProcess`
saves the `AccountDebited` event, transitions to the `AwaitingCreditConfirmation` state, spawns the `CreditAttempt`
actor and subsequently crashes - this will cause all children to be terminated, including the `CreditAttempt` actor that
was just created. When the `TransferProcess` is restarted, we reload all events and transition through to
the `AwaitingCreditConfirmation` state, where we are awaiting the response of the `CreditAttempt` actor that has been
terminated. No message will be received, _as children are not restarted._

One way to resolve this is by recreating the `CreditAttempt` actor when the `TransferProcess` receives the `Started`
event:

 ```c#
private async Task AwaitingCreditConfirmation(IContext context)
{
    switch (context.Message)
    {
        case Started _:
            context.SpawnNamed(TryCredit(_to, +_amount), "CreditAttempt");
            break;
            //..... other code omitted
    }
}
```

This restores the `CreditAttempt` actor and the `TransferProcess` actor is restored to the state it was in before the
crash.

### Why was my child Terminated?

Another problem with the current design is that `Terminated` messages are used to indicate that the `AccountProxy`
actors have failed their operations. This presents a problem when considering what happens when the `TransferProcess`
restarts. The `AccountProxy` actors are children of the `TransferProcess` actor, and one thing that happens during a
restart is that all children of an actor get stopped. So when the `TransferPorcess` actor is restarted, it receives
a `Terminated` message as a result of the `AccountProxy` child stopping. This now breaks our semantic meaning of what
the `Terminated` message represents - the `AccountProxy` hasn't failed to communicate with the `Account`,
the `TransferProcess` _itself_ has failed. Rather than redesign the `TransferProcess` we can track when this happens:

```c#
public async Task ReceiveAsync(IContext context)
{
    switch (context.Message)
    {
        //...
        case Stopping msg:
            _stopping = true;
            break;
        case Restarting msg:
            _restarting = true;
            break;
        case Terminated _ when _restarting || _stopping:
            return;
        //...
    }
    await _behavior.ReceiveAsync(context);
```

When the `TransferProcess` is restarted or stopped, it receives a message that we can handle. This happens _before_
children are stopped. By setting a flag when these events occur, we can safely ignore `Terminated` messages when in this
state. We return immediately from the `ReceiveAsync` method which prevents `await _behavior.ReceiveAsync(context)`
method from executing. These flags are reset to their default values of `false` when the restarting process has
finished.

___

## Part 4 - Supervision, error kernels and idempotency  <a name="4"></a>

An important factor in implementing a saga is the concept of retrying operations. You will have noticed that when
attempting our debit and credit operations, a separate actor is created to perform this operation. We can lean on the
supervision strategies inherant in actor systems to help retry failed operations. If anything goes wrong with this call,
this actor fails, and allows it's parent to act on the failure...As this actor is a child of the `TransferProcess`
actor, it is supervised by the `TransferProcess` actor. This allows us to use the error kernel pattern to push out the "
dangerous" operation of calling a remote service into a separate actor, and set up a supervision strategy for that
actor. In this case our supervision strategy says to retry 10 times (SupervisorDirective.Restart on any failure) then
give up and stop the actor:

  ```c#
  var retryAttempts = 10;
  var supervisionStrategy = new OneForOneStrategy((pid, reason) => 
      SupervisorDirective.Restart, retryAttempts)
  
  Actor.FromProducer(() => new TransferProcess(...)
      .WithChildSupervisorStrategy(supervisionStrategy);
  ```

### Idempotent Receiver

Given that we are retrying operations against the Accounts we need them to be idempotent receivers. In messaging
applications, idempotency is defined as

> a message that has the same effect whether it is received once or multiple times. This means that a message can safely
> be resent without causing any problems even if the receiver receives duplicates of the same
> message. [EIP](http://www.enterpriseintegrationpatterns.com/patterns/messaging/IdempotentReceiver.html)

Some messages are naturally idempotent, others are not. In this case incrementing or decrementing the balance of an
account is not naturally idempotent so we need to track the messages we've already processed.
As each time we create the `AccountProxy` it has the same PID, we can use this to track which messages have been
processed. When the `Account` actor receive a duplicate message, we can send the same response we did with the original
message:

  ```c#
  private readonly Dictionary<PID, object> _processedMessages = new Dictionary<PID, object>();
  
  public Task ReceiveAsync(IContext context)
  {
      switch (context.Message)
      {
          case Credit msg when _processedMessages.ContainsKey(msg.ReplyTo):
              replyTo.Tell(_processedMessages[replyTo]);
              return Actor.Done;
          case Credit msg:
              _balance += amount;
              _processedMessages.Add(replyTo, new OK());
              replyTo.Tell(new OK());
              return Actor.Done;
          //...
      }
      return Actor.Done;
  }
  ```

The first time we receive the `Credit` message we will fall into the second case statement above where we adjust the
balance of the account and record that we have processed the message by using the `ReplyTo` property for uniqueness (
this is the `PID` of the `AccountProxy` and is unique within a `TransferProcess`) and saving the response message. If we
receive the same message again, the first case statement will be triggered, and we respond with the original response.

NOTE there is scope for clearing out the `_processedMessages` collection when `TransferProcess` actors are terminated, a
technique employed in
the [Business Handshake pattern](https://github.com/ReactiveDesignPatterns/CodeSamples/blob/master/chapter15/src/main/scala/com/reactivedesignpatterns/chapter15/BusinessHandshake.scala)
in the book [Reactive Design Patterns](https://www.manning.com/books/reactive-design-patterns)

## Part 5 - Results  <a name="5"></a>

We've now finished implementing the Money Transfer Saga, but we need a way to run it, and a way to generate failures. To
run the saga, we're going to use a simple console app:

```c#
internal class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Starting");
        var random = new Random();
        var numberOfTransfers = 1000;
        var uptime = 99.99;
        var retryAttempts = 3;
        var refusalProbability = 0.01;
        var busyProbability = 0.05;
        var provider = new InMemoryProvider();

        var props = Actor.FromProducer(() => new Runner(numberOfTransfers, uptime, refusalProbability, busyProbability, retryAttempts, false))
            .WithChildSupervisorStrategy(new OneForOneStrategy((pid, reason) => SupervisorDirective.Restart, retryAttempts, null));
        
        Console.WriteLine("Spawning runner");
        var runner = Actor.SpawnNamed(props, "runner");
       
        Console.ReadLine();
    }
}
```

Here we setup some variables for use by the `Account` actors to simulate the various failure scenarios:

* `uptime` determines the probability of `Account` failures
* `refusalProbability` determines the probability that an `Account` will refuse a credit or debit request
* `busyProbability` determines the probability that an `Account` will return a `ServiceUnavailable` response

We also specify how many sagas we are going to run and how many retry attempts will be made. We then create a `Runner`
actor to run the sagas.

### Runner

The `Runner` actor is responsible for running sagas and gathering and reporting on the results. It implements a
scatter-gather pattern to spawn `TransferProcess` actors then reports when they are all complete:

 ```c#
 public Task ReceiveAsync(IContext context)
{
    switch (context.Message)
    {
        //...
        case Started _:
            var random = new Random();
            _inMemoryProvider = new InMemoryProvider();

            for (int i = 1; i <= _numberOfIterations; i++)
            {
                int j = i;
                var fromAccount = CreateAccount($"FromAccount{j}", random);
                var toAccount = CreateAccount($"ToAccount{j}", random);

                var transferProps = Actor.FromProducer(() => new TransferProcess(fromAccount, toAccount, 10,
                        _inMemoryProvider, $"Transfer Process {j}", random, _uptime))
                    .WithChildSupervisorStrategy(
                        new OneForOneStrategy((pid, reason) => SupervisorDirective.Restart, _retryAttempts,
                            null));

                var transfer = context.SpawnNamed(transferProps, $"Transfer Process {j}");
                _transfers.Add(transfer);
            
            }
            break;
    }
    return Actor.Done;
}

```

Once the `Runner` is started, it loops through the number of iterations and creates two `Account` actors and
a `TransferProcess` actor each time, adding the `TransferProcess` PID to a `_transfers` collection. The `Runner`
supervises the `TransferActor`, and is responsible for restarting it should it crash. Inside the `TransferProcess`
actor, a call to `context.Parent.Tell();` informs the `Runner` of the result. The `Runner` then waits to receive results
back from the `TransferProcess` actors:

```c#
 public Task ReceiveAsync(IContext context)
{
    switch (context.Message)
    {
        case SuccessResult msg:
            _successResults++;
            CheckForCompletion(msg.Pid);
            break;
        case UnknownResult msg:
            _unknownResults++;
            CheckForCompletion(msg.Pid);
            break;
        case FailedAndInconsistent msg:
            _failedAndInconsistentResults++;
            CheckForCompletion(msg.Pid);
            break;
        case FailedButConsistentResult msg:
            _failedButConsistentResults++;
            CheckForCompletion(msg.Pid);
            break;
            //...
    }
```

For each result type, a counter is incremented to track the different result types, then a completion check is performed
to determine if all sagas have finished. If so, the results are outputted:

```c#
private void CheckForCompletion(PID pid)
{
    _transfers.Remove(pid);
    
    var remaining = _transfers.Count;
    //... write status to console.
    
    if (remaining == 0)
    {
        //... output results
    }
}
```

## Some examples

So how do things look? Given good enough uptime and sufficient retry attempts, things look good:

```
RESULTS for 99.99% uptime, 0.01% chance of refusal, 0.05% of being busy and 3 retry attempts:

- 100% (1000/1000) successful transfers
- 0% (0/1000) failures leaving a consistent system
- 0% (0/1000) failures leaving an inconsistent system
- 0% (0/1000) unknown results
```

Even if we lower the uptime and increase the probability of being busy, things still look good:

```
RESULTS for 99% uptime, 0.01% chance of refusal, 0.1% of being busy and 3 retry attempts:
- 100% (1000/1000) successful transfers
- 0% (0/1000) failures leaving a consistent system
- 0% (0/1000) failures leaving an inconsistent system
- 0% (0/1000) unknown results
```

We have to significantly drop the uptime to start seeing something different:

```
RESULTS for 90% uptime, 0.01% chance of refusal, 0.1% of being busy and 3 retry attempts:
- 99.9% (999/1000) successful transfers
- 0.2% (2/1000) failures leaving a consistent system
- 0% (0/1000) failures leaving an inconsistent system
- 0% (0/1000) unknown results
```

Dropping the retry attempts significantly affects our results:

```
RESULTS for 90% uptime, 0.01% chance of refusal, 0.1% of being busy and 1 retry attempts:
- 92% (920/1000) successful transfers
- 0% (0/1000) failures leaving a consistent system
- 0% (0/1000) failures leaving an inconsistent system
- 8% (80/1000) unknown results
```

Dramatically _increasing_ the retry attempts allows us to cope with a very failure prone system:

```
RESULTS for 50% uptime, 0.01% chance of refusal, 0.1% of being busy and 15 retry attempts:
- 100% (1000/1000) successful transfers
- 0% (0/1000) failures leaving a consistent system
- 0% (0/1000) failures leaving an inconsistent system
- 0% (0/1000) unknown results
```

Increasing the probability of refusal has a big impact, as retrying does not happen:

```
RESULTS for 50% uptime, 20.1% chance of refusal, 0.2% of being busy and 15 retry attempts:
68.9% (689/1000) successful transfers
29.2% (292/1000) failures leaving a consistent system
4.6% (46/1000) failures leaving an inconsistent system
0.1% (1/1000) unknown results
```

The biggest effect comes from not retrying at all, as we are in danger of timing out on our requests (`Account` actor
has `Thread.Sleep(_random.Next(0,150)` in it, whilst the `AccountProxy` expects a response back within 100
milliseconds):

 ```
RESULTS for 99.99% uptime, 0.01% chance of refusal, 0.01% of being busy and 0 retry attempts:
48.8% (488/1000) successful transfers
0.1% (1/1000) failures leaving a consistent system
0% (0/1000) failures leaving an inconsistent system
51.1% (511/1000) unknown results
```

Overall the results show the importance of retrying our operations, and the need to have idempotent receivers that
enable us to retry. We can get very good results with very failure prone systems if we simply retry our operations.

This is of cource an artificial scenario. In the real world, you'd want more subtle retry strategies that allow remote
services to recover from high demand they might be experiencing or failures that might be transient - exponential
back-off strategies are more useful than immediate retries. The ability to be able to resume a saga from a given point
through the use of an audit log is also very important - if a remote service is down for a considerable amount of time
you can still attempt the saga when it has recovered.
