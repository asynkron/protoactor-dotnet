// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Diagnostics;
using System.Runtime;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Proto;

namespace SpawnBenchmark
{
    class Request
    {
        public long Div;
        public long Num;
        public long Size;
    }

    class MyActor : IActor
    {
        private readonly ActorSystem _system;
        private long _replies;
        private PID _replyTo;
        private long _sum;

        private MyActor(ActorSystem system) => _system = system;

        public Task ReceiveAsync(IContext context)
        {
            var msg = context.Message;

            switch (msg)
            {
                case Request {Size: 1} r:
                    context.Respond(r.Num);
                    context.Stop(context.Self);
                    return Task.CompletedTask;
                case Request r: {
                    _replies = r.Div;
                    _replyTo = context.Sender;

                    for (var i = 0; i < r.Div; i++)
                    {
                        var child = _system.Root.Spawn(Props(_system));
                        context.Request(child, new Request
                            {
                                Num = r.Num + i * (r.Size / r.Div),
                                Size = r.Size / r.Div,
                                Div = r.Div
                            }
                        );
                    }

                    return Task.CompletedTask;
                }
                case long res: {
                    _sum += res;
                    _replies--;

                    if (_replies == 0)
                    {
                        context.Send(_replyTo, _sum);
                        context.Stop(context.Self);
                    }
                    return Task.CompletedTask;
                }
                default:
                    return Task.CompletedTask;
            }
        }

        private static MyActor ProduceActor(ActorSystem system) => new(system);

        public static Props Props(ActorSystem system) => Proto.Props.FromProducer(() => ProduceActor(system));
    }

    [InProcess]
    public class SpawnBenchmark
    {
        private ActorSystem _system;
        private RootContext _context;

        [GlobalSetup]
        public void Setup()
        {
            _system = new ActorSystem();
            _context = _system.Root;
        }

        [GlobalCleanup]
        public Task Cleanup() => _system.ShutdownAsync();

        // [IterationSetup]
        // public void LogProcessCounts()
        // {
        //     Console.WriteLine("Actors:" + _system.ProcessRegistry.ProcessCount);
        // }

        [Benchmark]
        public async Task Benchmark()
        {
            var pid = _context.Spawn(MyActor.Props(_system));
            await _context.RequestAsync<long>(pid, new Request
                {
                    Num = 0,
                    Size = 1000000,
                    Div = 10
                }
            );
            _context.StopAsync(pid).Wait();
        }
    }
}