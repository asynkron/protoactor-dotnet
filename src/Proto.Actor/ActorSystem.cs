using JetBrains.Annotations;

namespace Proto
{
    [PublicAPI]
    public class ActorSystem
    {
        public static readonly ActorSystem Default = new ActorSystem();


        public ActorSystem()
        {
            ProcessRegistry = new ProcessRegistry(this);
            Root = new RootContext(this);
            DeadLetter = new DeadLetterProcess(this);
            Guardians = new Guardians(this);
            EventStream = new EventStream();
            var eventStreamProcess = new EventStreamProcess(this);
            ProcessRegistry.TryAdd("eventstream", eventStreamProcess);
            Plugins = new Plugins();
        }
        public ProcessRegistry ProcessRegistry { get; }
        public RootContext Root { get; }
        public Guardians Guardians { get; }
        public DeadLetterProcess DeadLetter { get; }
        public EventStream EventStream { get; }
        public Plugins Plugins { get; }
        public const string NoHost = "nonhost";
        private string _host = NoHost;
        private int _port;
        public string Address { get; private set; } = NoHost;
        public void SetAddress(string host, int port)
        {
            _host = host;
            _port = port;
            Address = $"{host}:{port}";
        }

        public (string Host, int Port) GetAddress() => (_host, _port);
    }
}