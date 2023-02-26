// -----------------------------------------------------------------------
// <copyright file="Skynet.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Proto;
// ReSharper disable MethodHasAsyncOverload

namespace ProtoActorBenchmarks;

[InProcess]
public class SkyNetBenchmark
{
    private IRootContext _context;
    private ActorSystem _actorSystem;

    [Params(0)]
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
    public Task SkyNetRequestAsync()
    {
        var pid = _context.Spawn(SkyNetRequestResponseActor.Props(_actorSystem));
        return _context.RequestAsync<long>(pid, new Request
            {
                Num = 0,
                Size = 1000000,
            }, CancellationToken.None
        );
    }

    [Benchmark]
    public Task SkyNetMessaging()
    {
        var pid = _context.Spawn(SkynetActor.Props(_actorSystem));
        return _context.RequestAsync<long>(pid, new Request
            {
                Num = 0,
                Size = 1000000,
            }, CancellationToken.None
        );
    }

    private class SkyNetRequestResponseActor : IActor
    {
        private readonly ActorSystem _system;

        private SkyNetRequestResponseActor(ActorSystem system) => _system = system;

        public async Task ReceiveAsync(IContext context)
        {
            if (context.Message is Request calc)
            {
                if (calc.Size == 1)
                {
                    context.Respond(calc.Num);
                    context.Stop(context.Self);
                    return;
                }

                var tasks = new Task<long>[10];
                var each = calc.Size / 10;

                for (var i = 0; i < 10; i++)
                {
                    var pid = _system.Root.Spawn(Props(_system));
                    tasks[i] = context.RequestAsync<long>(pid, new Request
                        {
                            Size = each,
                            Num = calc.Num + i * each
                        }
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

        private static SkyNetRequestResponseActor ProduceActor(ActorSystem system) => new(system);

        public static Props Props(ActorSystem system) => Proto.Props.FromProducer(() => ProduceActor(system));
    }

    class SkynetActor : IActor
    {
        private readonly ActorSystem _system;
        private long _replies;
        private PID _replyTo;
        private long _sum;

        private SkynetActor(ActorSystem system) => _system = system;

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
                    _replies = 10;
                    _replyTo = context.Sender;

                    for (var i = 0; i < 10; i++)
                    {
                        var child = _system.Root.Spawn(Props(_system));
                        context.Request(child, new Request
                            {
                                Num = r.Num + i * (r.Size / 10),
                                Size = r.Size / 10,
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

        private static SkynetActor ProduceActor(ActorSystem system) => new(system);

        public static Props Props(ActorSystem system) => Proto.Props.FromProducer(() => ProduceActor(system));
    }

    class Request
    {
        public long Num;
        public long Size;
    }
}