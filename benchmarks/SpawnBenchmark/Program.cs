// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
//  </copyright>
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
        private static MyActor ProduceActor(ActorSystem system) => new MyActor(system);
        public static Props Props(ActorSystem system) => Proto.Props.FromProducer(() => ProduceActor(system));
        private long _replies;
        private PID _replyTo;
        private long _sum;
        private readonly ActorSystem _system;

        private MyActor(ActorSystem system) => _system = system;

        public Task ReceiveAsync(IContext context)
        {
            var msg = context.Message;
            var r = msg as Request;
            if (r != null)
            {
                if (r.Size == 1)
                {
                    context.Respond(r.Num);
                    context.Stop(context.Self);
                    return Actor.Done;
                }
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
                    });
                }

                return Actor.Done;
            }
            if (msg is Int64 res)
            {
                _sum += res;
                _replies--;
                if (_replies == 0)
                {
                    context.Send(_replyTo, _sum);
                }
                return Actor.Done;
            }
            return Actor.Done;
        }
    }

    internal class Program
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
                });
                t.ConfigureAwait(false);
                var res = t.Result;
                Console.WriteLine(sw.Elapsed);
                Console.WriteLine(res);
                context.StopAsync(pid).Wait();
                Task.Delay(500).Wait();
            }
            //   Console.ReadLine();
        }
    }
}