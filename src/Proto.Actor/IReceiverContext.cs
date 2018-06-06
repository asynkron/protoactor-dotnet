using System.Threading.Tasks;

namespace Proto
{
    public interface IReceiverContext
    {
        Task Receive(MessageEnvelope envelope);
    }
}