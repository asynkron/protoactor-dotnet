// -----------------------------------------------------------------------
// <copyright file="ShortBenchmark.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Proto;
using Proto.Mailbox;

namespace ProtoActorBenchmarks;

[MemoryDiagnoser, InProcess]
public class ShortBenchmark
{
    private RootContext _context;
    private PID _echoActor;
    private Props _echoProps;
    private TimeSpan _timeout;

    [GlobalSetup]
    public void Setup()
    {
        _context = new RootContext(new ActorSystem());

        _echoProps = Props
            .FromProducer(() => new EchoActor2())
            .WithMailbox(() => BoundedMailbox.Create(2048));
        _echoActor = _context.Spawn(_echoProps);
        _timeout = TimeSpan.FromSeconds(5);
    }

    [Benchmark]
    public Task InProcessPingPong() => _context.RequestAsync<string>(_echoActor, "ping", _timeout);
}
