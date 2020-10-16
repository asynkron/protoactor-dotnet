using System;
using JetBrains.Annotations;

namespace Proto
{
    [PublicAPI]
    public class ActorSystem
    {
        public static readonly ActorSystem Default = new ActorSystem();
        internal const string NoHost = "nonhost";
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
            EventStream = new EventStream(config.DeadLetterThrottleInterval, config.DeadLetterThrottleCount);
            var eventStreamProcess = new EventStreamProcess(this);
            ProcessRegistry.TryAdd("eventstream", eventStreamProcess);
        }

        public string Address { get; private set; } = NoHost;

        public ActorSystemConfig Config { get; }
        
        public ProcessRegistry ProcessRegistry { get; }

        public RootContext Root { get; }

        public Guardians Guardians { get; }

        public DeadLetterProcess DeadLetter { get; }

        public EventStream EventStream { get; }

        public void SetAddress(string host, int port)
        {
            _host = host;
            _port = port;
            Address = $"{host}:{port}";
        }

        public (string Host, int Port) GetAddress() => (_host, _port);
    }
}