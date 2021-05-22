// -----------------------------------------------------------------------
// <copyright file="TransferFactory.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using Proto;
using Proto.Persistence;

namespace Saga.Factories
{
    class TransferFactory
    {
        private readonly double _availability;
        private readonly IContext _context;
        private readonly IProvider _provider;
        private readonly Random _random;
        private readonly int _retryAttempts;

        internal TransferFactory(
            IContext context,
            IProvider provider,
            Random random,
            double availability,
            int retryAttempts
        )
        {
            _random = random;
            _availability = availability;
            _retryAttempts = retryAttempts;
            _context = context;
            _provider = provider;
        }

        internal PID CreateTransfer(
            string actorName,
            PID fromAccount,
            PID toAccount,
            decimal amount,
            string persistenceId
        )
        {
            var transferProps = 
                Props.FromProducer(() =>
                    new TransferProcess(fromAccount, toAccount, amount, _provider, persistenceId, _random, _availability)
                )
                .WithChildSupervisorStrategy(
                    new OneForOneStrategy((_, _) => SupervisorDirective.Restart, _retryAttempts, null)
                );
            var transfer = _context.SpawnNamed(transferProps, actorName);
            return transfer;
        }
    }
}