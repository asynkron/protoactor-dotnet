using System.Threading.Tasks;

namespace Proto
{
    public interface IReceiverContext : IInfoContext
    {
        Task Receive(MessageEnvelope envelope);
    }
}