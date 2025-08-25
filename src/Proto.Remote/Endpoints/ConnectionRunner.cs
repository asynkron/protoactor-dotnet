namespace Proto.Remote;

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using Proto.Extensions;

public sealed class ConnectionRunner
{
    private readonly string _address;
    private readonly IEndpoint _endpoint;
    private readonly TimeSpan _backoff;
    private readonly int _maxRetries;
    private readonly IConnectionMode _mode;
    private readonly Random _random;
    private readonly RemoteConfig _remoteConfig;
    private readonly ActorSystem _system;
    private readonly CancellationToken _stopToken;
    private readonly Action _onConnected;
    private readonly Action _onDisconnected;
    private readonly Action<double> _recordWriteDuration;
    private readonly Action<string> _logInfo;
    private readonly Action<string> _logDebug;
    private readonly Action<string> _logWarning;
    private readonly Action<Exception, string> _logError;

    public ConnectionRunner(string address, IEndpoint endpoint, ActorSystem system, RemoteConfig remoteConfig,
        IConnectionMode mode, TimeSpan backoff, int maxRetries, Random random, CancellationToken stopToken,
        Action onConnected, Action onDisconnected, Action<double> recordWriteDuration,
        Action<string> logInfo, Action<string> logDebug, Action<string> logWarning,
        Action<Exception, string> logError)
    {
        _address = address;
        _endpoint = endpoint;
        _system = system;
        _remoteConfig = remoteConfig;
        _mode = mode;
        _backoff = backoff;
        _maxRetries = maxRetries;
        _random = random;
        _stopToken = stopToken;
        _onConnected = onConnected;
        _onDisconnected = onDisconnected;
        _recordWriteDuration = recordWriteDuration;
        _logInfo = logInfo;
        _logDebug = logDebug;
        _logWarning = logWarning;
        _logError = logError;
    }

    public async Task RunAsync()
    {
        string? actorSystemId = null;
        var rs = new RestartStatistics(0, null);

        while (!_stopToken.IsCancellationRequested)
        {
            var cts = new CancellationTokenSource();
            try
            {
                _logInfo($"[ServerConnector][{_system.Address}] Connecting to {_address}");

                var addressWithProtocol = $"{(_remoteConfig.UseHttps ? "https://" : "http://")}{_address}";
                var channel = GrpcChannel.ForAddress(addressWithProtocol, _remoteConfig.ChannelOptions);
                var client = new Remoting.RemotingClient(channel);
                using var call = client.Receive(_remoteConfig.CallOptions);

                _onConnected();

                await _mode.SendConnectRequest(_system, _remoteConfig, call).ConfigureAwait(false);

                await call.ResponseStream.MoveNext().ConfigureAwait(false);
                var response = call.ResponseStream.Current;
                if (response?.MessageTypeCase != RemoteMessage.MessageTypeOneofCase.ConnectResponse)
                {
                    throw new Exception("Expected ConnectResponse");
                }

                var connectResponse = response.ConnectResponse;
                if (connectResponse.Blocked)
                {
                    _logError(new Exception("Blocked"), $"[ServerConnector][{_system.Address}] Connection Refused to remote member {connectResponse.MemberId} address {_address}, we are blocked");
                    _system.Remote().BlockList.Block(new[] { _system.Id }, "Blocked by remote member");
                    var terminated = new EndpointTerminatedEvent(false, _address, _system.Id);
                    _system.EventStream.Publish(terminated);
                    return;
                }

                actorSystemId = connectResponse.MemberId;
                if (_system.Remote().BlockList.IsBlocked(actorSystemId))
                {
                    _logError(new Exception("Blocked"), $"[ServerConnector][{_system.Address}] Connection Refused to remote member {connectResponse.MemberId} address {_address}, they are blocked");
                    var terminated = new EndpointTerminatedEvent(false, _address, _system.Id);
                    _system.EventStream.Publish(terminated);
                    return;
                }

                rs.Reset();

                var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(_stopToken, cts.Token).Token;

                var writer = StartWriter(combinedToken, call, cts);
                var reader = StartReader(combinedToken, call, actorSystemId, cts);

                _logInfo($"[ServerConnector][{_system.Address}] Connected to {_address}");

                await writer.ConfigureAwait(false);
                cts.Cancel();
                await call.RequestStream.CompleteAsync().ConfigureAwait(false);
                await reader.ConfigureAwait(false);

                _onDisconnected();
                _logInfo($"[ServerConnector][{_system.Address}] Disconnected from {_address}");
            }
            catch (Exception e)
            {
                e.CheckFailFast();

                if (actorSystemId is not null && _system.Remote().BlockList.IsBlocked(actorSystemId))
                {
                    _logDebug($"[ServerConnector][{_system.Address}] dropped connection to blocked member {actorSystemId}/{_address}");
                    var terminated = new EndpointTerminatedEvent(true, _address, actorSystemId);
                    _system.EventStream.Publish(terminated);
                    break;
                }

                if (ShouldStop(rs))
                {
                    if (e is RpcException { StatusCode: StatusCode.Unavailable })
                    {
                        _logInfo($"[ServerConnector][{_system.Address}] Stopping connection to {_address} after retries expired because the endpoint is unavailable");
                    }
                    else
                    {
                        _logError(e, $"[ServerConnector][{_system.Address}] Stopping connection to {_address} after retries expired because of {e.GetType().Name}");
                    }

                    var terminated = new EndpointTerminatedEvent(true, _address, actorSystemId);
                    _system.EventStream.Publish(terminated);
                    break;
                }

                var backoff = rs.FailureCount * (int)_backoff.TotalMilliseconds;
                var noise = _random.Next(500);
                var duration = TimeSpan.FromMilliseconds(backoff + noise);
                await Task.Delay(duration).ConfigureAwait(false);
                _logWarning($"[ServerConnector][{_system.Address}] Restarting endpoint connection to {_address} after {duration} because of {e.GetType().Name} ({rs.FailureCount} / {_maxRetries})");
            }
            finally
            {
                cts.Cancel();
            }
        }
    }

    private Task StartWriter(CancellationToken token, AsyncDuplexStreamingCall<RemoteMessage, RemoteMessage> call, CancellationTokenSource cts)
    {
        return Task.Run(async () =>
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
                    _logDebug($"[ServerConnector][{_system.Address}] Writer cancelled for {_address}");
                }
            }
        });
    }

    private Task StartReader(CancellationToken token, AsyncDuplexStreamingCall<RemoteMessage, RemoteMessage> call, string actorSystemId, CancellationTokenSource cts)
    {
        return Task.Run(async () =>
        {
            try
            {
                while (await call.ResponseStream.MoveNext(token).ConfigureAwait(false))
                {
                    var currentMessage = call.ResponseStream.Current;
                    switch (currentMessage.MessageTypeCase)
                    {
                        case RemoteMessage.MessageTypeOneofCase.DisconnectRequest:
                            _logDebug($"[ServerConnector][{_system.Address}] Received disconnection request from {_address}");
                            var terminated = new EndpointTerminatedEvent(false, _address, actorSystemId);
                            _system.EventStream.Publish(terminated);
                            break;
                        default:
                            _mode.HandleMessage(currentMessage, _address);
                            break;
                    }
                }

                _logDebug($"[ServerConnector][{_system.Address}] Reader finished for {_address}");
            }
            catch (OperationCanceledException)
            {
                _logDebug($"[ServerConnector][{_system.Address}] Reader cancelled for {_address}");
            }
            catch (RpcException e) when (e.StatusCode == StatusCode.Cancelled)
            {
                _logWarning($"[ServerConnector][{_system.Address}] Reader cancelled for {_address}");
            }
            catch (Exception e)
            {
                _logWarning($"[ServerConnector][{_system.Address}] Error in reader for {_address} {e.GetType().Name}");
                cts.Cancel();
                throw;
            }
        });
    }

    private bool ShouldStop(RestartStatistics rs)
    {
        if (_maxRetries == 0)
        {
            return true;
        }

        rs.Fail();

        if (rs.FailureCount > _maxRetries)
        {
            rs.Reset();
            return true;
        }

        return false;
    }
}
