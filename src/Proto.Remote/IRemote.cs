using System.Threading.Tasks;
using Proto.Extensions;

namespace Proto.Remote
{
    public class RemoteExtension : IActorSystemExtension<RemoteExtension>
    {
        public IRemote Remote { get; }
        public RemoteExtension(IRemote remote)
        {
            Remote = remote;
        }
    }
    public interface IRemote 
    {
        RemoteConfigBase Config { get; }
        ActorSystem System { get; }
        bool Started { get; }

        Task ShutdownAsync(bool graceful = true);

        Task StartAsync();
    }
}