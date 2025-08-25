namespace Proto.Remote;

using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles incoming messages from the remote endpoint.
/// </summary>
internal sealed class ConnectionReader
{
    private readonly string _address;
    private readonly ActorSystem _system;
    private readonly IConnectionMode _mode;
    private readonly ILogger _logger;

    public ConnectionReader(string address, ActorSystem system, IConnectionMode mode, ILogger logger)
    {
        _address = address;
        _system = system;
        _mode = mode;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken token, AsyncDuplexStreamingCall<RemoteMessage, RemoteMessage> call, string actorSystemId, CancellationTokenSource cts)
    {
        try
        {
            await RemoteStreamProcessor.RunReaderAsync(
                call.ResponseStream,
                token,
                currentMessage =>
                {
                    switch (currentMessage.MessageTypeCase)
                    {
                        case RemoteMessage.MessageTypeOneofCase.DisconnectRequest:
                            _logger.ReceivedDisconnectionRequest(_system.Address, _address);
                            var terminated = new EndpointTerminatedEvent(false, _address, actorSystemId);
                            _system.EventStream.Publish(terminated);
                            break;
                        default:
                            _mode.HandleMessage(currentMessage, _address);
                            break;
                    }
                }).ConfigureAwait(false);

            _logger.ReaderFinished(_system.Address, _address);
        }
        catch (OperationCanceledException)
        {
            _logger.ReaderCancelledDebug(_system.Address, _address);
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Cancelled)
        {
            _logger.ReaderCancelledWarning(_system.Address, _address);
        }
        catch (Exception e)
        {
            _logger.ReaderError(_system.Address, _address, e.GetType().Name);
            cts.Cancel();
            throw;
        }
    }
}
