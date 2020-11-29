using System.Threading.Tasks;

namespace Proto.Remote
{
    public interface IRemote : Extensions.IActorSystemExtension<IRemote>
    {
        RemoteConfigBase Config { get; }
        ActorSystem System { get; }
        bool Started { get; }
        Task ShutdownAsync(bool graceful = true);
        Task StartAsync();
    }
}