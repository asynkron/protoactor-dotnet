// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Runtime;
using System.Threading.Tasks;
using Proto;

namespace SpawnBenchmark
{
    internal class Request
    {
        public long Div;
        public long Num;
        public long Size;
    }

    internal class MyActor : IActor
    {
        private readonly ActorSystem _system;
        private long _replies;
        private PID _replyTo;
        private long _sum;

        private MyActor(ActorSystem system) => _system = system;

        public Task ReceiveAsync(IContext context)
        {
            object? msg = context.Message;

            switch (msg)
            {
                case Request {Size: 1} r:
                    context.Respond(r.Num);
                    context.Stop(context.Self);
                    return Task.CompletedTask;
                case Request r:
                    {
                        _replies = r.Div;
                        _replyTo = context.Sender;

                        for (int i = 0; i < r.Div; i++)
                        {
                            PID child = _system.Root.Spawn(Props(_system));
                            context.Request(child,
                                new Request {Num = r.Num + i * (r.Size / r.Div), Size = r.Size / r.Div, Div = r.Div}
                            );
                        }

                        return Task.CompletedTask;
                    }
                case long res:
                    {
                        _sum += res;
                        _replies--;
                        if (_replies == 0)
                        {
                            context.Send(_replyTo, _sum);
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

    internal class Program
    {
        private static void Main()
        {
            ActorSystem system = new ActorSystem();
            RootContext context = new RootContext(system);

            while (true)
            {
                Console.WriteLine($"Is Server GC {GCSettings.IsServerGC}");

                PID pid = context.Spawn(MyActor.Props(system));
                Stopwatch sw = Stopwatch.StartNew();
                Task<long> t = context.RequestAsync<long>(pid, new Request {Num = 0, Size = 1000000, Div = 10}
                );
                t.ConfigureAwait(false);
                long res = t.Result;
                Console.WriteLine(sw.Elapsed);
                Console.WriteLine(res);
                context.StopAsync(pid).Wait();
                Task.Delay(500).Wait();
            }

            //   Console.ReadLine();
        }
    }
}
