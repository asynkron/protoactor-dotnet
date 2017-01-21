using System.Threading.Tasks;

namespace Proto
{
    public partial class PID
    {
        public PID(string address, string id)
        {
            Address = address;
            Id = id;
        }

        internal Process Ref { get; set; }

        public void Tell(object message)
        {
            var reff = Ref ?? ProcessRegistry.Instance.Get(this);
            reff.SendUserMessage(this, message, null);
        }

        public void SendSystemMessage(SystemMessage sys)
        {
            var reff = Ref ?? ProcessRegistry.Instance.Get(this);
            reff.SendSystemMessage(this, sys);
        }

        public void Request(object message, PID sender)
        {
            var reff = Ref ?? ProcessRegistry.Instance.Get(this);
            reff.SendUserMessage(this, message, sender);
        }

        public Task<T> RequestAsync<T>(object message)
        {
            var tsc = new TaskCompletionSource<T>();
            var p = Actor.FromProducer(() => new FutureActor<T>(tsc));
            var fpid = Actor.Spawn(p);
            Request(message,fpid);
            return tsc.Task;
        }

        public void Stop()
        {
            var reff = ProcessRegistry.Instance.Get(this);
            reff.Stop(this);
        }

        public string ToShortString()
        {
            return Address + "/" + Id;
        }
    }
}