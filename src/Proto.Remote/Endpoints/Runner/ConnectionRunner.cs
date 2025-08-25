namespace Proto.Remote;

using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger _logger;
    private readonly ConnectionWriter _writer;
    private readonly ConnectionReader _reader;

    public ConnectionRunner(string address, IEndpoint endpoint, ActorSystem system, RemoteConfig remoteConfig,
        IConnectionMode mode, TimeSpan backoff, int maxRetries, Random random, CancellationToken stopToken,
        Action onConnected, Action onDisconnected, Action<double> recordWriteDuration, ILogger logger)
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
        _logger = logger;

        _writer = new ConnectionWriter(address, endpoint, system, remoteConfig, recordWriteDuration, logger);
        _reader = new ConnectionReader(address, system, mode, logger);
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
                _logger.Connecting(_system.Address, _address);

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
                    _logger.ConnectionRefusedWeAreBlocked(new Exception("Blocked"), _system.Address, connectResponse.MemberId, _address);
                    _system.Remote().BlockList.Block(new[] { _system.Id }, "Blocked by remote member");
                    var terminated = new EndpointTerminatedEvent(false, _address, _system.Id);
                    _system.EventStream.Publish(terminated);
                    return;
                }

                actorSystemId = connectResponse.MemberId;
                if (_system.Remote().BlockList.IsBlocked(actorSystemId))
                {
                    _logger.ConnectionRefusedTheyAreBlocked(new Exception("Blocked"), _system.Address, connectResponse.MemberId, _address);
                    var terminated = new EndpointTerminatedEvent(false, _address, _system.Id);
                    _system.EventStream.Publish(terminated);
                    return;
                }

                rs.Reset();

                var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(_stopToken, cts.Token).Token;

                var writer = _writer.RunAsync(combinedToken, call, cts);
                var reader = _reader.RunAsync(combinedToken, call, actorSystemId, cts);

                _logger.Connected(_system.Address, _address);

                await writer.ConfigureAwait(false);
                cts.Cancel();
                await call.RequestStream.CompleteAsync().ConfigureAwait(false);
                await reader.ConfigureAwait(false);

                _onDisconnected();
                _logger.Disconnected(_system.Address, _address);
            }
            catch (Exception e)
            {
                e.CheckFailFast();

                if (actorSystemId is not null && _system.Remote().BlockList.IsBlocked(actorSystemId))
                {
                    _logger.DroppedBlockedConnection(_system.Address, actorSystemId, _address);
                    var terminated = new EndpointTerminatedEvent(true, _address, actorSystemId);
                    _system.EventStream.Publish(terminated);
                    break;
                }

                if (ShouldStop(rs))
                {
                    if (e is RpcException { StatusCode: StatusCode.Unavailable })
                    {
                        _logger.StoppingUnavailable(_system.Address, _address);
                    }
                    else
                    {
                        _logger.StoppingBecauseException(e, _system.Address, _address, e.GetType().Name);
                    }

                    var terminated = new EndpointTerminatedEvent(true, _address, actorSystemId);
                    _system.EventStream.Publish(terminated);
                    break;
                }

                var backoff = rs.FailureCount * (int)_backoff.TotalMilliseconds;
                var noise = _random.Next(500);
                var duration = TimeSpan.FromMilliseconds(backoff + noise);
                await Task.Delay(duration).ConfigureAwait(false);
                _logger.RestartingEndpoint(_system.Address, _address, duration, e.GetType().Name, rs.FailureCount, _maxRetries);
            }
            finally
            {
                cts.Cancel();
            }
        }
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
