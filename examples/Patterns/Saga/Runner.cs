using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Proto;
using Saga.Messages;

namespace Saga
{
    public class Runner : IActor
    {
        private readonly int _numberOfIterations;
        private readonly double _uptime;
        private readonly double _refusalProbability;
        private readonly double _busyProbability;
        private readonly int _retryAttempts;
        private readonly bool _outputEventStream;
        private readonly HashSet<PID> _transfers = new HashSet<PID>();
        private int _successResults;
        private int _failedAndInconsistentResults;
        private int _failedButConsistentResults;
        private int _unknownResults;
        private InMemoryProvider _inMemoryProvider;

        public Runner(int numberOfIterations, double uptime, double refusalProbability, double busyProbability, int retryAttempts, bool outputEventStream)
        {
            _numberOfIterations = numberOfIterations;
            _uptime = uptime;
            _refusalProbability = refusalProbability;
            _busyProbability = busyProbability;
            _retryAttempts = retryAttempts;
            _outputEventStream = outputEventStream;
        }

        private PID CreateAccount(string name, Random random)
        {
            var accountProps = Actor.FromProducer(() => new Account(name, _uptime, _refusalProbability, _busyProbability, random));
            return Actor.SpawnNamed(accountProps, name);
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
                    
                        if (_numberOfIterations >= 10)
                        {
                            if (j % (_numberOfIterations / 10) == 0)
                                Console.WriteLine($"Started {j}/{_numberOfIterations} processes");
                        }
                        else
                        {
                            Console.WriteLine($"Started {j}/{_numberOfIterations} processes");
                        }
                    }
                    break;
            }
            return Actor.Done;
        }

        private void CheckForCompletion(PID pid)
        {
            _transfers.Remove(pid);
            
            var remaining = _transfers.Count;
            if (_numberOfIterations >= 10)
            {
                Console.Write(".");
                if (remaining % (_numberOfIterations / 10) == 0)
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
                    $"RESULTS for {_uptime}% uptime, {_refusalProbability}% chance of refusal, {_busyProbability}% of being busy and {_retryAttempts} retry attempts:");
                Console.WriteLine(
                    $"{AsPercentage(_numberOfIterations, _successResults)}% ({_successResults}/{_numberOfIterations}) successful transfers");
                Console.WriteLine(
                    $"{AsPercentage(_numberOfIterations, _failedButConsistentResults)}% ({_failedButConsistentResults}/{_numberOfIterations}) failures leaving a consistent system");
                Console.WriteLine(
                    $"{AsPercentage(_numberOfIterations, _failedAndInconsistentResults)}% ({_failedAndInconsistentResults}/{_numberOfIterations}) failures leaving an inconsistent system");
                Console.WriteLine(
                    $"{AsPercentage(_numberOfIterations, _unknownResults)}% ({_unknownResults}/{_numberOfIterations}) unknown results");
                
                if (_outputEventStream)
                {
                    foreach (var stream in _inMemoryProvider.Events)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"Event log for {stream.Key}");
                        foreach (var @event in stream.Value)
                        {
                            Console.WriteLine(@event.Value);
                        }
                    }
                }
            }
        }

        private double AsPercentage(double numberOfIterations, double results)
        {
            return (results / numberOfIterations) * 100;
        }
    }
}