// -----------------------------------------------------------------------
// <copyright file="Skynet.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Proto;

namespace ProtoActorBenchmarks
{
    [InProcess]
    public class SkyNetBenchmark
    {
        private RootContext _context;
        private ActorSystem _actorSystem;

        [Params(0, 5000)]
        public int SharedFutures { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            var config = ActorSystemConfig.Setup();

            if (SharedFutures > 0)
            {
                config = config.WithSharedFutures(SharedFutures);
            }

            _actorSystem = new ActorSystem(config);
            _context = _actorSystem.Root;
        }

        [GlobalCleanup]
        public void Cleanup() => _actorSystem.ShutdownAsync();

        [Benchmark]
        public Task SkyNetTest()
        {
            var pid = _context.Spawn(SkyNetActor.Props);
            return _context.RequestAsync<long>(pid, new Calculate(1_000_000, 0), CancellationToken.None);
        }

        private class SkyNetActor : IActor
        {
            public static readonly Props Props = Props.FromProducer(() => new SkyNetActor());

            public async Task ReceiveAsync(IContext context)
            {
                if (context.Message is Calculate calc)
                {
                    if (calc.Count <= 1)
                    {
                        context.Respond((long) calc.From);
                        return;
                    }

                    var tasks = new Task<long>[10];
                    var each = calc.Count / 10;
                    var to = calc.From + calc.Count;

                    for (var i = 0; i < 10; i++)
                    {
                        var pid = context.Spawn(Props);
                        tasks[i] = context.RequestAsync<long>(pid, new Calculate(each, calc.From + i * each)
                        );
                    }

                    await Task.WhenAll(tasks);
                    var tot = 0L;

                    for (var i = 0; i < tasks.Length; i++)
                    {
                        tot += tasks[i].Result;
                    }

                    context.Respond(tot);
                    context.Stop(context.Self);
                }
            }
        }

        readonly struct Calculate
        {
            public Calculate(int count, int from)
            {
                Count = count;
                From = from;
            }

            public int Count { get; }
            public int From { get; }
        }
    }
}