// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
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
        public static Props props = Actor.FromProducer(() => new MyActor());
        private long _replies;
        private PID _replyTo;
        private long _sum;

        public async Task ReceiveAsync(IContext context)
        {
            var msg = context.Message;
            var r = msg as Request;
            if (r != null)
            {
                if (r.Size == 1)
                {
                    await context.RespondAsync(r.Num);
                    await context.Self.StopAsync();
                }
                _replies = r.Div;
                _replyTo = context.Sender;
                for (var i = 0; i < r.Div; i++)
                {
                    var child = Actor.Spawn(props);
                    await child.RequestAsync(new Request
                    {
                        Num = r.Num + i * (r.Size / r.Div),
                        Size = r.Size / r.Div,
                        Div = r.Div
                    }, context.Self);
                }
            }
            if (msg is Int64)
            {
                _sum += (Int64) msg;
                _replies--;
                if (_replies == 0)
                {
                    await _replyTo.SendAsync(_sum);
                }
            }
        }
    }

    internal class Program
    {
        private static void Main()
        {
            Console.WriteLine($"Is Server GC {GCSettings.IsServerGC}");

            var pid = Actor.Spawn(MyActor.props);
            var sw = Stopwatch.StartNew();
            var t = pid.RequestAsync<long>(new Request
            {
                Num = 0,
                Size = 1000000,
                Div = 10
            });
            t.ConfigureAwait(false);
            var res = t.Result;
            Console.WriteLine(sw.Elapsed);
            Console.WriteLine(res);
            //   Console.ReadLine();
        }
    }
}