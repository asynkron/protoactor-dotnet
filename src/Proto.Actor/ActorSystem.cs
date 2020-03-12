namespace Proto
{
    public class ActorSystem
    {
        public ProcessRegistry ProcessRegistry { get; } 
        public RootContext Root { get; }

        public Guardians Guardians { get; }

        public Middleware Middleware { get;  }
        
        public DeadLetterProcess DeadLetter { get; }

        public ActorSystem()
        {
            ProcessRegistry = new ProcessRegistry(this);
            Root = new RootContext(this);
            DeadLetter = new DeadLetterProcess(this);
            Guardians = new Guardians(this);
            Middleware = new Middleware(this);
        }
        
        public PID DefaultSpawner(string name, Props props, PID parent)
        {
            var mailbox = props.MailboxProducer();
            var dispatcher = props.Dispatcher;
            var process = new ActorProcess(this,mailbox);
            var (pid, absent) = ProcessRegistry.TryAdd(name, process);
            if (!absent)
            {
                throw new ProcessNameExistException(name, pid);
            }

            var ctx = new ActorContext(props, parent, pid);
            mailbox.RegisterHandlers(ctx, dispatcher);
            mailbox.PostSystemMessage(Started.Instance);
            mailbox.Start();

            return pid;
        }

    }
}