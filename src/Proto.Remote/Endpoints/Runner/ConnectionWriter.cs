namespace Proto.Remote;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Proto.Extensions;
using System.Linq;

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
        while (!token.IsCancellationRequested)
        {
            while (_endpoint.OutgoingStash.TryPop(out var messages))
            {
                var batch = MessageBatchFactory.CreateBatch(_system, _remoteConfig, messages);
                try
                {
                    var sw = Stopwatch.StartNew();
                    await call.RequestStream.WriteAsync(new RemoteMessage { MessageBatch = batch }, token).ConfigureAwait(false);
                    sw.Stop();
                    _recordWriteDuration(sw.Elapsed.TotalSeconds);
                }
                catch (Exception)
                {
                    _ = _endpoint.OutgoingStash.Append(messages);
                    cts.Cancel();
                    throw;
                }
            }

            try
            {
                await foreach (var messages in _endpoint.Outgoing.Reader.ReadAllAsync(token).ConfigureAwait(false))
                {
                    var batch = MessageBatchFactory.CreateBatch(_system, _remoteConfig, messages);
                    try
                    {
                        var sw = Stopwatch.StartNew();
                        await call.RequestStream.WriteAsync(new RemoteMessage { MessageBatch = batch }, token).ConfigureAwait(false);
                        sw.Stop();
                        _recordWriteDuration(sw.Elapsed.TotalSeconds);
                    }
                    catch (Exception)
                    {
                        _ = _endpoint.OutgoingStash.Append(messages);
                        cts.Cancel();
                        throw;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.WriterCancelled(_system.Address, _address);
            }
        }
    }
}
