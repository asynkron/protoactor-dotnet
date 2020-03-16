using System.Threading.Tasks;

namespace Proto
{
    public static class Middleware
    {
        internal static Task Receive(IReceiverContext context, MessageEnvelope envelope) => context.Receive(envelope);

        internal static Task Sender(ActorSystem system, ISenderContext context, PID target, MessageEnvelope envelope)
        {
            target.SendUserMessage(system, envelope);
            return Actor.Done;
        }
    }
}