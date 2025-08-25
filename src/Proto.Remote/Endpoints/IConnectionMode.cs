namespace Proto.Remote;

using System.Threading.Tasks;
using Grpc.Core;

public interface IConnectionMode
{
    Task SendConnectRequest(ActorSystem system, RemoteConfig remoteConfig, AsyncDuplexStreamingCall<RemoteMessage, RemoteMessage> call);
    void HandleMessage(RemoteMessage message, string address);
}
