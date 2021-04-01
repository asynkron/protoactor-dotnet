// -----------------------------------------------------------------------
// <copyright file="LongBenchmark.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Proto;
using Proto.Mailbox;

namespace ProtoActorBenchmarks
{
    [MemoryDiagnoser]
    [InProcess]
    public class LongBenchmark
    {
        private RootContext _context;

        [Params(300, 400, 500, 600, 700, 800, 900)]
        public int Tps { get; set; }

        [Params(1000000)] public int MessageCount { get; set; }

        [Params(100)] public int BatchSize { get; set; }

        [GlobalSetup]
        public void Setup() => _context = new RootContext(new ActorSystem());

        [Benchmark]
        public Task InProcessPingPong()
        {
            ThreadPoolDispatcher d = new ThreadPoolDispatcher {Throughput = Tps};

            int clientCount = Environment.ProcessorCount * 1;
            PID[] clients = new PID[clientCount];
            PID[] echos = new PID[clientCount];
            TaskCompletionSource<bool>[] completions = new TaskCompletionSource<bool>[clientCount];

            Props echoProps = Props.FromProducer(() => new EchoActor())
                .WithDispatcher(d)
                .WithMailbox(() => BoundedMailbox.Create(2048));

            for (int i = 0; i < clientCount; i++)
            {
                TaskCompletionSource<bool> tsc = new TaskCompletionSource<bool>();
                completions[i] = tsc;

                Props clientProps = Props.FromProducer(() => new PingActor(tsc, MessageCount, BatchSize))
                    .WithDispatcher(d)
                    .WithMailbox(() => BoundedMailbox.Create(2048));

                clients[i] = _context.Spawn(clientProps);
                echos[i] = _context.Spawn(echoProps);
            }

            Task<bool>[] tasks = completions.Select(tsc => tsc.Task).ToArray();

            for (int i = 0; i < clientCount; i++)
            {
                PID client = clients[i];
                PID echo = echos[i];

                _context.Send(client, new Start(echo));
            }

            return Task.WhenAll(tasks);
        }
    }
}
