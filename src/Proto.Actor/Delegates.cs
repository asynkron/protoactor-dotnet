using System.Threading.Tasks;

namespace Proto
{
    public delegate Task Receive(IContext context);
    
    //TODO: IReceiveContext ?
    public delegate Task Receiver(IContext context, MessageEnvelope envelope);
    
    public delegate Task Sender(ISenderContext ctx, PID target, MessageEnvelope envelope);
}