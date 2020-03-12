using System.Threading.Tasks;

namespace Proto
{
    public class Middleware
    {
        public ActorSystem System { get;  }
        public Middleware(ActorSystem system)
        {
            System = system;
        }
        internal Task Receive(IReceiverContext context, MessageEnvelope envelope) => context.Receive(envelope);

        internal Task Sender(ISenderContext context, PID target, MessageEnvelope envelope)
        {
            target.SendUserMessage(System, envelope);
            return Actor.Done;
        }
    }
}