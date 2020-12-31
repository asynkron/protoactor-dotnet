// -----------------------------------------------------------------------
// <copyright file="ActorSystem.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Proto.Extensions;

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
            EventStream = new EventStream(config.DeadLetterThrottleInterval, config.DeadLetterThrottleCount, Shutdown);
            var eventStreamProcess = new EventStreamProcess(this);
            ProcessRegistry.TryAdd("eventstream", eventStreamProcess);
            Extensions = new ActorSystemExtensions(this);
        }

        public string Id { get; } = Guid.NewGuid().ToString("N");

        public string Address { get; private set; } = NoHost;

        public ActorSystemConfig Config { get; }

        public ProcessRegistry ProcessRegistry { get; }

        public RootContext Root { get; }

        public Guardians Guardians { get; }

        public DeadLetterProcess DeadLetter { get; }

        public EventStream EventStream { get; }

        public ActorSystemExtensions Extensions { get; }

        public CancellationToken Shutdown => _cts.Token;

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