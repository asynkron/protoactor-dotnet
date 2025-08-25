namespace Proto.Remote;

using System;
using System.Threading.Tasks;
using Grpc.Core;

public sealed class ServerConnectionMode : IConnectionMode
{
    private readonly Action<RemoteMessage, string> _onUnexpectedMessage;

    public ServerConnectionMode(Action<RemoteMessage, string> onUnexpectedMessage)
        => _onUnexpectedMessage = onUnexpectedMessage;

    public Task SendConnectRequest(ActorSystem system, RemoteConfig remoteConfig, AsyncDuplexStreamingCall<RemoteMessage, RemoteMessage> call)
        => call.RequestStream.WriteAsync(new RemoteMessage
        {
            ConnectRequest = new ConnectRequest
            {
                ServerConnection = new ServerConnection
                {
                    Address = system.Address,
                    MemberId = system.Id,
                    BlockList = { system.Remote().BlockList.BlockedMembers }
                }
            }
        });

    public void HandleMessage(RemoteMessage message, string address)
        => _onUnexpectedMessage(message, address);
}
