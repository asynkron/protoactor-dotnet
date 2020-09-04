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

        public ActorSystem()
        {
            ProcessRegistry = new ProcessRegistry(this);
            Root = new RootContext(this);
            DeadLetter = new DeadLetterProcess(this);
            Guardians = new Guardians(this);
            EventStream = new EventStream();
            var eventStreamProcess = new EventStreamProcess(this);
            ProcessRegistry.TryAdd("eventstream", eventStreamProcess);
        }

        public string Address { get; private set; } = NoHost;

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