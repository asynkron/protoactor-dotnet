using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Proto.Mailbox;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Proto.Actor.Benchmarks
{
    public class EchoActor2 : IActor
    {
        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case string _:
                    context.Send(context.Sender, "pong");
                    break;
            }
            return Task.CompletedTask;
        }
    }

    [MemoryDiagnoser, InProcess]
    public class ShortBenchmark
    {
        private RootContext _context;
        private Props _echoProps;
        private PID _echoActor;
        private TimeSpan _timeout;

        [GlobalSetup]
        public void Setup()
        {
            _context = new RootContext();

            _echoProps = Props.FromProducer(() => new EchoActor2())
                .WithMailbox(() => BoundedMailbox.Create(2048));
            _echoActor = _context.Spawn(_echoProps);
            _timeout = TimeSpan.FromSeconds(5);
        }

        [Benchmark]
        public Task InProcessPingPong()
        {
            return _context.RequestAsync<string>(_echoActor, "ping", _timeout);
        }
    }

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
        public void Setup()
        {
            _context = new RootContext();
        }

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

    class Program
    {
        static void Main(string[] args)
        {
            BenchmarkRunner.Run<ShortBenchmark>();
        }
    }
}
