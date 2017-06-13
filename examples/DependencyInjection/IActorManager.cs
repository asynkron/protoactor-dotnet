using System.Threading.Tasks;

namespace DependencyInjection
{
    public interface IActorManager
    {
        Task ActivateAsync();
    }
}