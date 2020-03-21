namespace Proto
{
    public class ActorSystem
    {
        public ProcessRegistry ProcessRegistry { get; }
        
        public RootContext Root { get; }

        public Guardians Guardians { get; }

        public DeadLetterProcess DeadLetter { get; }

        public EventStream EventStream { get; }

        public ActorSystem()
        {
            ProcessRegistry = new ProcessRegistry(this);
            Root = new RootContext(this);
            DeadLetter = new DeadLetterProcess(this);
            Guardians = new Guardians(this);
            EventStream = new EventStream();
        }
        
        public static ActorSystem Default = new ActorSystem();
    }
}