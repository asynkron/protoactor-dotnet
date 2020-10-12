using System.Threading.Tasks;
// ReSharper disable once CheckNamespace
namespace Proto
{
    public interface IReceiverContext : IInfoContext
    {
        Task Receive(MessageEnvelope envelope);
    }
}