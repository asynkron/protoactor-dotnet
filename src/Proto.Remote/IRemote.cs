using System.Threading.Tasks;
using Proto.Extensions;

namespace Proto.Remote
{
    public interface IRemote : IActorSystemExtension<IRemote>
    {
        RemoteConfigBase Config { get; }
        ActorSystem System { get; }
        bool Started { get; }

        Task ShutdownAsync(bool graceful = true);

        Task StartAsync();
    }
}