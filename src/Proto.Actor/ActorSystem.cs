// -----------------------------------------------------------------------
// <copyright file="ActorSystem.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Proto.Extensions;
using Proto.Future;
using Proto.Metrics;
using Proto.Utils;

namespace Proto
{
    [PublicAPI]
    public class ActorSystem
    {
        internal const string NoHost = "nonhost";
        private CancellationTokenSource _cts = new();
        private string _host = NoHost;
        private int _port;

        public ActorSystem() : this(new ActorSystemConfig())
        {
        }

        public ActorSystem(ActorSystemConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            ProcessRegistry = new ProcessRegistry(this);
            Root = new RootContext(this);
            DeadLetter = new DeadLetterProcess(this);
            Guardians = new Guardians(this);
            EventStream = new EventStream(this);
            Metrics = new ProtoMetrics(config.MetricsProviders);
            ProcessRegistry.TryAdd("eventstream", new EventStreamProcess(this));
            Extensions = new ActorSystemExtensions(this);
            DeferredFuture = new Lazy<FutureFactory>(() => new FutureFactory(this, config.SharedFutures, config.SharedFutureSize));

            RunThreadPoolStats();
        }

        public string Id { get; } = Guid.NewGuid().ToString("N");

        public string Address { get; private set; } = NoHost;

        public ActorSystemConfig Config { get; }

        public ProcessRegistry ProcessRegistry { get; }

        public RootContext Root { get; }

        public Guardians Guardians { get; }

        public DeadLetterProcess DeadLetter { get; }

        public EventStream EventStream { get; }

        public ProtoMetrics Metrics { get; }

        public ActorSystemExtensions Extensions { get; }

        private Lazy<FutureFactory> DeferredFuture { get; }

        internal FutureFactory Future => DeferredFuture.Value;

        public CancellationToken Shutdown => _cts.Token;

        private void RunThreadPoolStats()
        {
            var logger = Log.CreateLogger(nameof(ThreadPoolStats));
            _ = ThreadPoolStats.Run(TimeSpan.FromSeconds(5),
                t => {
                    //collect the latency metrics
                    Metrics.InternalActorMetrics.ThreadPoolLatencyHistogram.Observe(t, new[] {Id, Address});

                    //does it take longer than 1 sec for a task to start executing?
                    if (t > TimeSpan.FromSeconds(1)) logger.LogWarning("ThreadPool is running hot, Threadpool latency {ThreadPoolLatency}", t);
                }, _cts.Token
            );
        }

        public Task ShutdownAsync()
        {
            _cts.Cancel();
            return Task.CompletedTask;
        }

        public void SetAddress(string host, int port)
        {
            _host = host;
            _port = port;
            Address = $"{host}:{port}";
        }

        public RootContext NewRoot(MessageHeader? headers = null, params Func<Sender, Sender>[] middleware) =>
            new(this, headers, middleware);

        public (string Host, int Port) GetAddress() => (_host, _port);
    }
}