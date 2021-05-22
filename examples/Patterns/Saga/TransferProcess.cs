// -----------------------------------------------------------------------
// <copyright file="TransferProcess.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using Proto;
using Proto.Persistence;
using Saga.Messages;

namespace Saga
{
    class TransferProcess : IActor
    {
        private readonly decimal _amount;
        private readonly double _availability;
        private readonly Behavior _behavior = new();
        private readonly PID _from;
        private readonly Persistence _persistence;
        private readonly string _persistenceId;
        private readonly Random _random;
        private readonly PID _to;
        private bool _processCompleted;
        private bool _restarting;
        private bool _stopping;

        public TransferProcess(
            PID from,
            PID to,
            decimal amount,
            IProvider provider,
            string persistenceId,
            Random random,
            double availability
        )
        {
            _from = from;
            _to = to;
            _amount = amount;
            _persistenceId = persistenceId;
            _random = random;
            _availability = availability;
            _persistence = Persistence.WithEventSourcing(provider, persistenceId, ApplyEvent);
        }

        public async Task ReceiveAsync(IContext context)
        {
            var message = context.Message;
            Console.WriteLine($"[{_persistenceId}] Receiving :{message}");

            switch (message)
            {
                case Started:
                    // default to Starting behavior
                    _behavior.Become(Starting);

                    // recover state from persistence - if there are any events, the current behavior 
                    // should change
                    await _persistence.RecoverStateAsync();
                    break;
                case Stopping:
                    _stopping = true;
                    break;
                case Restarting:
                    _restarting = true;
                    break;
                case Stopped _ when !_processCompleted:
                    await _persistence.PersistEventAsync(new TransferFailed("Unknown. Transfer Process crashed"));
                    await _persistence.PersistEventAsync(
                        new EscalateTransfer("Unknown failure. Transfer Process crashed")
                    );
                    context.Send(context.Parent!, new UnknownResult(context.Self));
                    return;
                case Terminated _ when _restarting || _stopping:
                    // if the TransferProcess itself is restarting or stopping due to failure, we will receive a
                    // Terminated message for any child actors due to them being stopped but we should not
                    // treat this as a failure of the saga, so return here to stop further processing
                    return;
                default:
                    // simulate failures of the transfer process itself
                    if (Fail()) throw new Exception();

                    break;
            }

            // pass through all messages to the current behavior. Note this includes the Started message we
            // may have just handled as what we should do when started depends on the current behavior
            await _behavior.ReceiveAsync(context);
        }

        private static Props TryCredit(PID targetActor, decimal amount) => Props
            .FromProducer(() => new AccountProxy(targetActor, sender => new ChangeBalance.Credit(amount, sender)));

        private static Props TryDebit(PID targetActor, decimal amount) => Props
            .FromProducer(() => new AccountProxy(targetActor, sender => new ChangeBalance.Debit(amount, sender)));

        private void ApplyEvent(Event @event)
        {
            Console.WriteLine($"Applying event: {@event.Data}");

            switch (@event.Data)
            {
                case TransferStarted:
                    _behavior.Become(AwaitingDebitConfirmation);
                    break;
                case AccountDebited:
                    _behavior.Become(AwaitingCreditConfirmation);
                    break;
                case CreditRefused:
                    _behavior.Become(RollingBackDebit);
                    break;
                case AccountCredited:
                case DebitRolledBack:
                case TransferFailed:
                    _processCompleted = true;
                    break;
            }
        }

        private bool Fail()
        {
            var comparison = _random.NextDouble() * 100;
            return comparison > _availability;
        }

        private async Task Starting(IContext context)
        {
            if (context.Message is Started)
            {
                context.SpawnNamed(TryDebit(_from, -_amount), "DebitAttempt");
                await _persistence.PersistEventAsync(new TransferStarted());
            }
        }

        private async Task AwaitingDebitConfirmation(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    // if we are in this state when restarted then we need to recreate the TryDebit actor
                    context.SpawnNamed(TryDebit(_from, -_amount), "DebitAttempt");
                    break;
                case OK _:
                    // good to proceed to the credit
                    await _persistence.PersistEventAsync(new AccountDebited());
                    context.SpawnNamed(TryCredit(_to, +_amount), "CreditAttempt");
                    break;
                case Refused _:
                    // the debit has been refused, and should not be retried 
                    await _persistence.PersistEventAsync(new TransferFailed("Debit refused"));
                    context.Send(context.Parent!, new Result.FailedButConsistentResult(context.Self));
                    StopAll(context);
                    break;
                case Terminated _:
                    // the actor that is trying to make the debit has failed to respond with success
                    // we dont know why
                    await _persistence.PersistEventAsync(new StatusUnknown());
                    StopAll(context);
                    break;
            }
        }

        private async Task AwaitingCreditConfirmation(IContext context)
        {
            switch (context.Message)
            {
                case Started:
                    // if we are in this state when started then we need to recreate the TryCredit actor
                    context.SpawnNamed(TryCredit(_to, +_amount), "CreditAttempt");
                    break;
                case OK:
                    var fromBalance =
                        await context.RequestAsync<decimal>(_from, new GetBalance(), TimeSpan.FromMilliseconds(2000));
                    var toBalance =
                        await context.RequestAsync<decimal>(_to, new GetBalance(), TimeSpan.FromMilliseconds(2000));

                    await _persistence.PersistEventAsync(new AccountCredited());
                    await _persistence.PersistEventAsync(new TransferCompleted(_from, fromBalance, _to, toBalance));
                    context.Send(context.Parent!, new Result.SuccessResult(context.Self));
                    StopAll(context);
                    break;
                case Refused:

                    // sometimes a remote service might say it refuses to perform some operation. 
                    // This is different from a failure
                    await _persistence.PersistEventAsync(new CreditRefused());

                    // we have definitely debited the _from account as it was confirmed, and we 
                    // haven't credited to _to account, so try and rollback
                    context.SpawnNamed(TryCredit(_from, +_amount), "RollbackDebit");

                    break;
                case Terminated:
                    // at this point, we do not know if the credit succeeded. The remote account has not 
                    // confirmed success, but it might have succeeded then crashed, or failed to respond.
                    // Given that we don't know, just fail + escalate
                    await _persistence.PersistEventAsync(new StatusUnknown());
                    StopAll(context);
                    break;
            }
        }

        private async Task RollingBackDebit(IContext context)
        {
            switch (context.Message)
            {
                case Started:
                    // if we are in this state when started then we need to recreate the TryCredit actor
                    context.SpawnNamed(TryCredit(_from, +_amount), "RollbackDebit");
                    break;
                case OK:
                    await _persistence.PersistEventAsync(new DebitRolledBack());
                    await _persistence.PersistEventAsync(new TransferFailed($"Unable to rollback debit to {_to.Id}"));
                    context.Send(context.Parent!, new Result.FailedAndInconsistent(context.Self));
                    StopAll(context);
                    break;
                case Refused: // in between making the credit and debit, the _from account has started refusing!! :O
                case Terminated:
                    await _persistence.PersistEventAsync(
                        new TransferFailed($"Unable to rollback process. {_from.Id} is owed {_amount}")
                    );
                    await _persistence.PersistEventAsync(new EscalateTransfer($"{_from.Id} is owed {_amount}"));
                    context.Send(context.Parent!, new Result.FailedAndInconsistent(context.Self));
                    StopAll(context);
                    break;
            }
        }

        private void StopAll(IContext context)
        {
            context.Stop(_from);
            context.Stop(_to);
            context.Stop(context.Self);
        }
    }
}