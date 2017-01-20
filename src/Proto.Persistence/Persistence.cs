using System.Threading.Tasks;
using Google.Protobuf;

namespace Proto.Persistence
{
    public class Persistence
    {
        public IProviderState State { get; set; }
        public int EventIndex { get; set; }
        public IContext Context { get; set; }
        public bool Recovering { get; set; }
        public string Name => Context.Self.Id;

        public void Init(IProvider provider, IContext context)
        {
            State = provider.GetState();
            Context = context;
            Recovering = true;
            
            State.Restart();
            var t = State.GetSnapshot(Name);
            if (t != null)
            {
                EventIndex = t.Item2;
                Context.Receive(t.Item1);
            }
            State.GetEvents(Name, EventIndex, e =>
            {
                Context.Receive(e);
                EventIndex++;
            });
        }

        public void PersistReceive(IMessage message)
        {
            State.PersistEvent(Name, EventIndex, message);
            EventIndex++;
            Context.Receive(message);
            if (State.GetSnapshotInterval() == 0)
            {
                Context.Receive(new RequestSnapshot());
            }
        }

        public void PersistSnapshot(IMessage snapshot)
        {
            State.PersistSnapshot(Name, EventIndex, snapshot);
        }

        public static Receive Using(IProvider provider)
        {
            return context =>
            {
                switch (context.Message)
                {
                    case Started started:
                        break;
                    case Replay replay:
                        var p = context.Actor as IPersistentActor;
                        if (p != null)
                        {
                            p.Persistence = new Persistence();
                            p.Persistence.Init(provider, context);
                        }
                        break;
                }
                return Actor.Done;
            };
        }
    }

    public class RequestSnapshot
    {
    }
}
