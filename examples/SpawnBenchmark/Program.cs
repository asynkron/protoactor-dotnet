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
        private static MyActor ProduceActor() => new MyActor();
        public static Props Props = Props.FromProducer(ProduceActor);
        private long _replies;
        private PID _replyTo;
        private long _sum;

        public Task ReceiveAsync(IContext context)
        {
            var msg = context.Message;
            var r = msg as Request;
            if (r != null)
            {
                if (r.Size == 1)
                {
                    context.Respond(r.Num);
                    context.Self.Stop();
                    return Actor.Done;
                }
                _replies = r.Div;
                _replyTo = context.Sender;
                for (var i = 0; i < r.Div; i++)
                {
                    var child = RootContext.Empty.Spawn(Props);
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
            var context = new RootContext();
            while (true)
            {
                Console.WriteLine($"Is Server GC {GCSettings.IsServerGC}");

                var pid = context.Spawn(MyActor.Props);
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
                pid.StopAsync().Wait();
                Task.Delay(500).Wait();
            }
            //   Console.ReadLine();
        }
    }
}