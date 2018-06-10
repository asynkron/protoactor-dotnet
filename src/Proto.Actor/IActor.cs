using System.Threading.Tasks;

namespace Proto
{
    public interface IActor
    {
        Task ReceiveAsync(IContext context);
    }
}