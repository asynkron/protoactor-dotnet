using Proto;
using Proto.Persistence;
using System;

namespace Saga.Factories
{
    internal class TransferFactory
    {
        private readonly Random _random;
        private readonly double _availability;
        private readonly int _retryAttempts;
        private readonly IContext _context;
        private readonly IProvider _provider;

        internal TransferFactory(IContext context, IProvider provider, Random random, double availability, int retryAttempts)
        {
            this._random = random;
            this._availability = availability;
            this._retryAttempts = retryAttempts;
            this._context = context;
            this._provider = provider;
        }

        internal PID CreateTransfer(string actorName, PID fromAccount, PID toAccount, decimal amount, string persistenceId)
        {
            var transferProps = Props.FromProducer(() =>
                new TransferProcess(fromAccount, toAccount, amount, _provider, persistenceId, _random, _availability))
                    .WithChildSupervisorStrategy(
                        new OneForOneStrategy((pid, reason) => SupervisorDirective.Restart, _retryAttempts, null));
            var transfer = _context.SpawnNamed(transferProps, actorName);
            return transfer;
        }
    }
}
