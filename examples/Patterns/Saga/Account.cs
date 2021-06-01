// -----------------------------------------------------------------------
// <copyright file="Account.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Proto;
using Saga.Messages;

namespace Saga
{
    class Account : IActor
    {
        private readonly double _busyProbability;
        private readonly string _name;
        private readonly Dictionary<PID, object> _processedMessages = new();
        private readonly Random _random;
        private readonly double _refusalProbability;
        private readonly double _serviceUptime;
        private decimal _balance = 10;

        public Account(
            string name,
            double serviceUptime,
            double refusalProbability,
            double busyProbability,
            Random random
        )
        {
            _name = name;
            _serviceUptime = serviceUptime;
            _refusalProbability = refusalProbability;
            _busyProbability = busyProbability;
            _random = random;
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case ChangeBalance.Credit(_, var replyTo) when AlreadyProcessed(replyTo):
                    return Reply(replyTo);
                case ChangeBalance.Credit(var amount, var replyTo):
                    return AdjustBalance(context, replyTo, amount);
                case ChangeBalance.Debit(_, var replyTo) when AlreadyProcessed(replyTo):
                    return Reply(replyTo);
                case ChangeBalance.Debit(var amount, var replyTo) when amount + _balance >= 0:
                    return AdjustBalance(context, replyTo, amount);
                case ChangeBalance.Debit msg:
                    context.Send(msg.ReplyTo, new InsufficientFunds());
                    break;
                case GetBalance:
                    context.Respond(_balance);
                    break;
            }

            return Task.CompletedTask;

            Task Reply(PID replyTo)
            {
                context.Send(replyTo, _processedMessages[replyTo]);
                return Task.CompletedTask;
            }
        }

        /// <summary>
        ///     We want to simulate the following:
        ///     * permanent refusals to process the message
        ///     * temporary refusals to process the message
        ///     * failures before updating the balance
        ///     * failures after updating the balance
        ///     * slow processing
        ///     * successful processing
        /// </summary>
        private Task AdjustBalance(IContext context, PID replyTo, decimal amount)
        {
            if (RefusePermanently())
            {
                _processedMessages.Add(replyTo, new Refused());
                context.Send(replyTo, new Refused());
            }

            if (Busy())
                context.Send(replyTo, new ServiceUnavailable());

            // generate the behavior to be used whilst processing this message
            var behaviour = DetermineProcessingBehavior();

            if (behaviour == Behavior.FailBeforeProcessing)
                return Failure();

            // simulate potential long-running process
            Thread.Sleep(_random.Next(0, 150));

            _balance += amount;
            _processedMessages.Add(replyTo, new OK());

            // simulate chance of failure after applying the change. This will
            // force a retry of the operation which will test the operation
            // is idempotent
            if (behaviour == Behavior.FailAfterProcessing)
                return Failure();

            context.Send(replyTo, new OK());
            return Task.CompletedTask;

            Task Failure()
            {
                context.Send(replyTo, new InternalServerError());
                return Task.CompletedTask;
            }
        }

        private bool Busy()
        {
            var comparison = _random.NextDouble() * 100;
            return comparison <= _busyProbability;
        }

        private bool RefusePermanently()
        {
            var comparison = _random.NextDouble() * 100;
            return comparison <= _refusalProbability;
        }

        private Behavior DetermineProcessingBehavior()
        {
            var comparision = _random.NextDouble() * 100;

            if (comparision > _serviceUptime)
                return _random.NextDouble() * 100 > 50 ? Behavior.FailBeforeProcessing : Behavior.FailAfterProcessing;

            return Behavior.ProcessSuccessfully;
        }

        private bool AlreadyProcessed(PID replyTo) => _processedMessages.ContainsKey(replyTo);

        private enum Behavior
        {
            FailBeforeProcessing,
            FailAfterProcessing,
            ProcessSuccessfully
        }
    }
}