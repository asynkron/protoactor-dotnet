namespace Proto.Remote;

using System.Threading.Tasks;
using Grpc.Core;

public sealed class ClientConnectionMode : IConnectionMode
{
    private readonly RemoteMessageHandler _handler;

    public ClientConnectionMode(RemoteMessageHandler handler) => _handler = handler;

    public Task SendConnectRequest(ActorSystem system, RemoteConfig remoteConfig, AsyncDuplexStreamingCall<RemoteMessage, RemoteMessage> call)
        => call.RequestStream.WriteAsync(new RemoteMessage
        {
            ConnectRequest = new ConnectRequest
            {
                ClientConnection = new ClientConnection
                {
                    MemberId = system.Id
                }
            }
        });

    public void HandleMessage(RemoteMessage message, string address)
        => _handler.HandleRemoteMessage(message, address);
}
