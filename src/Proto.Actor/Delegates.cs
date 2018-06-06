using System.Threading.Tasks;

namespace Proto
{
    
    //TODO: Task Receive(IReceiveContext context);
    public delegate Task Receive(IContext context);

    
    public delegate Task Sender(ISenderContext ctx, PID target, MessageEnvelope envelope);
}