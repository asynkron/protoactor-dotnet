namespace Proto.Remote;

using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Proto.Extensions;

/// <summary>
/// Handles sending message batches to the remote endpoint.
/// </summary>
internal sealed class ConnectionWriter
{
    private readonly string _address;
    private readonly IEndpoint _endpoint;
    private readonly RemoteConfig _remoteConfig;
    private readonly ActorSystem _system;
    private readonly Action<double> _recordWriteDuration;
    private readonly ILogger _logger;

    public ConnectionWriter(string address, IEndpoint endpoint, ActorSystem system, RemoteConfig remoteConfig, Action<double> recordWriteDuration, ILogger logger)
    {
        _address = address;
        _endpoint = endpoint;
        _system = system;
        _remoteConfig = remoteConfig;
        _recordWriteDuration = recordWriteDuration;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken token, AsyncDuplexStreamingCall<RemoteMessage, RemoteMessage> call, CancellationTokenSource cts)
    {
        try
        {
            await RemoteStreamProcessor.RunWriterAsync(
                _endpoint,
                _system,
                _remoteConfig,
                (m, ct) => call.RequestStream.WriteAsync(m, ct),
                token,
                cts,
                _recordWriteDuration).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.WriterCancelled(_system.Address, _address);
        }
    }
}
