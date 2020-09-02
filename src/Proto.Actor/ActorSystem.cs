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
    }
}