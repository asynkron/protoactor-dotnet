using System;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Proto.Mailbox;

namespace Proto.Actor.Benchmarks
{
    [MemoryDiagnoser, InProcess]
    public class LongBenchmark
    {
        private RootContext _context;

        [Params(300, 400, 500, 600, 700, 800, 900)]
        public int Tps { get; set; }

        [Params(1000000)]
        public int MessageCount { get; set; }

        [Params(100)]
        public int BatchSize { get; set; }

        [GlobalSetup]
        public void Setup() => _context = new RootContext(new ActorSystem());

        [Benchmark]
        public Task InProcessPingPong()
        {
            var d = new ThreadPoolDispatcher { Throughput = Tps };

            var clientCount = Environment.ProcessorCount * 1;
            var clients = new PID[clientCount];
            var echos = new PID[clientCount];
            var completions = new TaskCompletionSource<bool>[clientCount];

            var echoProps = Props.FromProducer(() => new EchoActor())
                .WithDispatcher(d)
                .WithMailbox(() => BoundedMailbox.Create(2048));

            for (var i = 0; i < clientCount; i++)
            {
                var tsc = new TaskCompletionSource<bool>();
                completions[i] = tsc;

                var clientProps = Props.FromProducer(() => new PingActor(tsc, MessageCount, BatchSize))
                    .WithDispatcher(d)
                    .WithMailbox(() => BoundedMailbox.Create(2048));

                clients[i] = _context.Spawn(clientProps);
                echos[i] = _context.Spawn(echoProps);
            }

            var tasks = completions.Select(tsc => tsc.Task).ToArray();

            for (var i = 0; i < clientCount; i++)
            {
                var client = clients[i];
                var echo = echos[i];

                _context.Send(client, new Start(echo));
            }

            return Task.WhenAll(tasks);
        }
    }
}