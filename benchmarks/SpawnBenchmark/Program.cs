﻿// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Diagnostics;
using System.Runtime;
using System.Threading.Tasks;
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

    class Program
    {
        private static void Main()
        {
            var system = new ActorSystem();
            var context = new RootContext(system);

            while (true)
            {
                Console.WriteLine($"Is Server GC {GCSettings.IsServerGC}");

                var pid = context.Spawn(MyActor.Props(system));
                var sw = Stopwatch.StartNew();
                var t = context.RequestAsync<long>(pid, new Request
                    {
                        Num = 0,
                        Size = 1000000,
                        Div = 10
                    }
                );
                t.ConfigureAwait(false);
                var res = t.Result;
                Console.WriteLine(sw.Elapsed);
                Console.WriteLine(res);
                Task.Delay(500).Wait();
            }

            //   Console.ReadLine();
        }
    }
}