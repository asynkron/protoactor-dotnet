using System.Threading.Tasks;

namespace Proto
{
    public delegate Task Receive(IContext context);

    //TODO: IReceiveContext ?
    public delegate Task Receiver(IReceiverContext context, MessageEnvelope envelope);

    public delegate Task Sender(ActorSystem system, ISenderContext context, PID target, MessageEnvelope envelope);
}