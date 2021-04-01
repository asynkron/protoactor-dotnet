// -----------------------------------------------------------------------
// <copyright file="Runner.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Proto;
using Saga.Factories;
using Saga.Internal;
using Saga.Messages;

namespace Saga
{
    public class Runner : IActor
    {
        private readonly double _busyProbability;
        private readonly int _intervalBetweenConsoleUpdates;
        private readonly int _numberOfIterations;
        private readonly double _refusalProbability;
        private readonly int _retryAttempts;
        private readonly HashSet<PID> _transfers = new HashSet<PID>();
        private readonly double _uptime;
        private readonly bool _verbose;
        private int _failedAndInconsistentResults;
        private int _failedButConsistentResults;
        private InMemoryProvider _inMemoryProvider;
        private int _successResults;
        private int _unknownResults;

        public Runner(
            int numberOfIterations,
            int intervalBetweenConsoleUpdates,
            double uptime,
            double refusalProbability,
            double busyProbability,
            int retryAttempts,
            bool verbose
        )
        {
            _numberOfIterations = numberOfIterations;
            _intervalBetweenConsoleUpdates = intervalBetweenConsoleUpdates;
            _uptime = uptime;
            _refusalProbability = refusalProbability;
            _busyProbability = busyProbability;
            _retryAttempts = retryAttempts;
            _verbose = verbose;
        }

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
                case Started _:
                    Random random = new Random();
                    _inMemoryProvider = new InMemoryProvider();
                    new ForWithProgress(_numberOfIterations, _intervalBetweenConsoleUpdates, true, false).EveryNth(
                        i => Console.WriteLine($"Started {i}/{_numberOfIterations} processes"),
                        (i, nth) =>
                        {
                            int j = i;
                            PID fromAccount = CreateAccount(context, $"FromAccount{j}", random);
                            PID toAccount = CreateAccount(context, $"ToAccount{j}", random);
                            string actorName = $"Transfer Process {j}";
                            string persistanceID = $"Transfer Process {j}";
                            TransferFactory factory = new TransferFactory(context, _inMemoryProvider, random, _uptime,
                                _retryAttempts
                            );
                            PID transfer = factory.CreateTransfer(actorName, fromAccount, toAccount, 10, persistanceID);
                            _transfers.Add(transfer);
                            if (i == _numberOfIterations && !nth)
                            {
                                Console.WriteLine($"Started {j}/{_numberOfIterations} proesses");
                            }
                        }
                    );
                    break;
            }

            return Task.CompletedTask;
        }

        private PID CreateAccount(IContext context, string name, Random random)
        {
            Props accountProps = Props.FromProducer(() =>
                new Account(name, _uptime, _refusalProbability, _busyProbability, random)
            );
            return context.SpawnNamed(accountProps, name);
        }

        private void CheckForCompletion(PID pid)
        {
            _transfers.Remove(pid);

            int remaining = _transfers.Count;

            if (_numberOfIterations >= _intervalBetweenConsoleUpdates)
            {
                Console.Write(".");

                if (remaining % (_numberOfIterations / _intervalBetweenConsoleUpdates) == 0)
                {
                    Console.WriteLine();
                    Console.WriteLine($"{remaining} processes remaining");
                }
            }
            else
            {
                Console.WriteLine($"{remaining} processes remaining");
            }

            if (remaining == 0)
            {
                Thread.Sleep(250);
                Console.WriteLine();
                Console.WriteLine(
                    $"RESULTS for {_uptime}% uptime, {_refusalProbability}% chance of refusal, {_busyProbability}% of being busy and {_retryAttempts} retry attempts:"
                );
                Console.WriteLine(
                    $"{AsPercentage(_numberOfIterations, _successResults)}% ({_successResults}/{_numberOfIterations}) successful transfers"
                );
                Console.WriteLine(
                    $"{AsPercentage(_numberOfIterations, _failedButConsistentResults)}% ({_failedButConsistentResults}/{_numberOfIterations}) failures leaving a consistent system"
                );
                Console.WriteLine(
                    $"{AsPercentage(_numberOfIterations, _failedAndInconsistentResults)}% ({_failedAndInconsistentResults}/{_numberOfIterations}) failures leaving an inconsistent system"
                );
                Console.WriteLine(
                    $"{AsPercentage(_numberOfIterations, _unknownResults)}% ({_unknownResults}/{_numberOfIterations}) unknown results"
                );

                if (_verbose)
                {
                    foreach (KeyValuePair<string, Dictionary<long, object>> stream in _inMemoryProvider.Events)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"Event log for {stream.Key}");

                        foreach (KeyValuePair<long, object> @event in stream.Value)
                        {
                            Console.WriteLine(@event.Value);
                        }
                    }
                }
            }
        }

        private double AsPercentage(double numberOfIterations, double results) => results / numberOfIterations * 100;
    }
}
