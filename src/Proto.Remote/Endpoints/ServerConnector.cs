// -----------------------------------------------------------------------
//   <copyright file="ServerConnector.cs" company="Asynkron AB">
//       Copyright (C) 2015-2022 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Proto.Remote.Metrics;

namespace Proto.Remote;

public sealed class ServerConnector
{
    public enum Type
    {
        ServerSide,
        ClientSide
    }

    private readonly string _address;
    private readonly TimeSpan _backoff;
    private readonly IChannelProvider _channelProvider;
    private readonly Type _connectorType;
    private readonly CancellationTokenSource _cts = new();
    private readonly IEndpoint _endpoint;

    private readonly ILogger _logger = Log.CreateLogger<ServerConnector>();
    private readonly int _maxNrOfRetries;
    private readonly KeyValuePair<string, object?>[] _metricTags = Array.Empty<KeyValuePair<string, object?>>();
    private readonly Random _random = new();
    private readonly RemoteConfigBase _remoteConfig;
    private readonly RemoteMessageHandler _remoteMessageHandler;
    private readonly Task _runner;
    private readonly ActorSystem _system;

    public ServerConnector(string address, Type connectorType, IEndpoint endpoint, IChannelProvider channelProvider,
        ActorSystem system, RemoteConfigBase remoteConfig, RemoteMessageHandler remoteMessageHandler)
    {
        _channelProvider = channelProvider;
        _system = system;
        _remoteConfig = remoteConfig;
        _remoteMessageHandler = remoteMessageHandler;
        _address = address;
        _connectorType = connectorType;
        _endpoint = endpoint;
        _maxNrOfRetries = remoteConfig.EndpointWriterOptions.MaxRetries;
        _backoff = remoteConfig.EndpointWriterOptions.RetryBackOff;
        _runner = Task.Run(RunAsync);

        if (_system.Metrics.Enabled)
        {
            _metricTags = new KeyValuePair<string, object?>[]
                { new("id", _system.Id), new("address", _system.Address) };
        }
    }

    public async Task Stop()
    {
        _cts.Cancel();
        await _runner.ConfigureAwait(false);
    }

    public async Task RunAsync()
    {
        string? actorSystemId = null;
        var rs = new RestartStatistics(0, null);

        while (!_cts.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("[ServerConnector][{SystemAddress}] Connecting to {Address}", _system.Address,
                    _address);

                var channel = _channelProvider.GetChannel(_address);
                var client = new Remoting.RemotingClient(channel);
                using var call = client.Receive(_remoteConfig.CallOptions);

                if (_system.Metrics.Enabled)
                {
                    RemoteMetrics.RemoteEndpointConnectedCount
                        .Add(1, new KeyValuePair<string, object?>("id", _system.Id),
                            new KeyValuePair<string, object?>("address", _system.Address),
                            new KeyValuePair<string, object?>("destinationaddress", _address));
                }

                switch (_connectorType)
                {
                    case Type.ServerSide:
                        await call.RequestStream.WriteAsync(new RemoteMessage
                            {
                                ConnectRequest = new ConnectRequest
                                {
                                    ServerConnection = new ServerConnection
                                    {
                                        Address = _system.Address,
                                        MemberId = _system.Id,
                                        BlockList = { _system.Remote().BlockList.BlockedMembers }
                                    }
                                }
                            })
                            .ConfigureAwait(false);

                        break;
                    case Type.ClientSide:
                        await call.RequestStream.WriteAsync(new RemoteMessage
                            {
                                ConnectRequest = new ConnectRequest
                                {
                                    ClientConnection = new ClientConnection
                                    {
                                        MemberId = _system.Id
                                    }
                                }
                            })
                            .ConfigureAwait(false);

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                await call.ResponseStream.MoveNext().ConfigureAwait(false);
                var response = call.ResponseStream.Current;

                if (response?.MessageTypeCase != RemoteMessage.MessageTypeOneofCase.ConnectResponse)
                {
                    throw new Exception("Expected ConnectResponse");
                }

                var connectResponse = response.ConnectResponse;

                if (connectResponse.Blocked)
                {
                    _logger.LogError(
                        "[ServerConnector][{SystemAddress}] Connection Refused to remote member {MemberId} address {Address}, we are blocked",
                        _system.Address, connectResponse.MemberId, _address);

                    //block self
                    _system.Remote().BlockList.Block(new[] { _system.Id });
                    var terminated = new EndpointTerminatedEvent(false, _address, _system.Id);
                    _system.EventStream.Publish(terminated);

                    return;
                }

                actorSystemId = connectResponse.MemberId;

                if (_system.Remote().BlockList.IsBlocked(actorSystemId))
                {
                    _logger.LogError(
                        "[ServerConnector][{SystemAddress}] Connection Refused to remote member {MemberId} address {Address}, they are blocked",
                        _system.Address, connectResponse.MemberId, _address);

                    var terminated = new EndpointTerminatedEvent(false, _address, _system.Id);
                    _system.EventStream.Publish(terminated);

                    return;
                }

                rs.Reset();
                var cancellationTokenSource = new CancellationTokenSource();

                var combinedToken = CancellationTokenSource
                    .CreateLinkedTokenSource(_cts.Token, cancellationTokenSource.Token)
                    .Token;

                var writer = Task.Run(async () =>
                {
                    while (!combinedToken.IsCancellationRequested)
                    {
                        while (_endpoint.OutgoingStash.TryPop(out var message))
                        {
                            try
                            {
                                await call.RequestStream.WriteAsync(message).ConfigureAwait(false);
                            }
                            catch (Exception)
                            {
                                _ = _endpoint.OutgoingStash.Append(message);
                                cancellationTokenSource.Cancel();

                                throw;
                            }
                        }

                        try
                        {
                            await foreach (var message in _endpoint.Outgoing.Reader.ReadAllAsync(combinedToken)
                                               .ConfigureAwait(false))
                            {
                                try
                                {
                                    if (_system.Metrics.Enabled)
                                    {
                                        var sw = Stopwatch.StartNew();
                                        await call.RequestStream.WriteAsync(message).ConfigureAwait(false);
                                        sw.Stop();
                                        RemoteMetrics.RemoteWriteDuration.Record(sw.Elapsed.TotalSeconds, _metricTags);
                                    }
                                    else
                                    {
                                        await call.RequestStream.WriteAsync(message).ConfigureAwait(false);
                                    }
                                }
                                catch (Exception)
                                {
                                    _ = _endpoint.OutgoingStash.Append(message);
                                    cancellationTokenSource.Cancel();

                                    throw;
                                }
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            _logger.LogDebug("[ServerConnector][{SystemAddress}] Writer cancelled for {Address}",
                                _system.Address, _address);
                        }
                    }
                });

                var reader = Task.Run(async () =>
                {
                    try
                    {
                        while (await call.ResponseStream.MoveNext().ConfigureAwait(false))
                        {
                            // if (_endpoint.CancellationToken.IsCancellationRequested) continue;
                            var currentMessage = call.ResponseStream.Current;

                            switch (currentMessage.MessageTypeCase)
                            {
                                case RemoteMessage.MessageTypeOneofCase.DisconnectRequest:
                                {
                                    _logger.LogDebug(
                                        "[ServerConnector][{SystemAddress}] Received disconnection request from {Address}",
                                        _system.Address, _address);

                                    var terminated = new EndpointTerminatedEvent(false, _address, actorSystemId);
                                    _system.EventStream.Publish(terminated);

                                    break;
                                }
                                default:
                                    if (_connectorType == Type.ServerSide)
                                    {
                                        _logger.LogWarning(
                                            "[ServerConnector][{SystemAddress}] Received {Message} from {_address}",
                                            _system.Address, currentMessage, _address);
                                    }
                                    else
                                    {
                                        _remoteMessageHandler.HandleRemoteMessage(currentMessage, _address);
                                    }

                                    break;
                            }
                        }

                        _logger.LogDebug("[ServerConnector][{SystemAddress}] Reader finished for {Address}",
                            _system.Address, _address);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogDebug("[ServerConnector][{SystemAddress}] Reader cancelled for {Address}",
                            _system.Address, _address);
                    }
                    catch (RpcException e) when (e.StatusCode == StatusCode.Cancelled)
                    {
                        _logger.LogWarning("[ServerConnector][{SystemAddress}] Reader cancelled for {Address}",
                            _system.Address, _address);
                    }
                    catch (Exception e)
                    {
                        _logger.LogWarning("[ServerConnector][{SystemAddress}] Error in reader for {Address} {Reason}",
                            _system.Address, _address, e.GetType().Name);

                        cancellationTokenSource.Cancel();

                        throw;
                    }
                });

                _logger.LogInformation("[ServerConnector][{SystemAddress}] Connected to {Address}", _system.Address,
                    _address);

                await writer.ConfigureAwait(false);
                cancellationTokenSource.Cancel();
                await call.RequestStream.CompleteAsync().ConfigureAwait(false);
                await reader.ConfigureAwait(false);

                if (_system.Metrics.Enabled)
                {
                    RemoteMetrics.RemoteEndpointDisconnectedCount.Add(1, _metricTags);
                }

                _logger.LogInformation("[ServerConnector][{SystemAddress}] Disconnected from {Address}",
                    _system.Address, _address);
            }
            catch (Exception e)
            {
                e.CheckFailFast();

                if (actorSystemId is not null && _system.Remote().BlockList.IsBlocked(actorSystemId))
                {
                    _logger.LogDebug(
                        "[ServerConnector][{SystemAddress}] dropped connection to blocked member {ActorSystemId}/{Address}",
                        _system.Address, actorSystemId, _address);

                    var terminated = new EndpointTerminatedEvent(true, _address, actorSystemId);
                    _system.EventStream.Publish(terminated);

                    break;
                }

                if (ShouldStop(rs))
                {
                    _logger.LogError(e,
                        "[ServerConnector][{SystemAddress}] Stopping connection to {Address} after retries expired because of {Reason}",
                        _system.Address, _address, e.GetType().Name);

                    var terminated = new EndpointTerminatedEvent(true, _address, actorSystemId);
                    _system.EventStream.Publish(terminated);

                    break;
                }

                var backoff = rs.FailureCount * (int)_backoff.TotalMilliseconds;
                var noise = _random.Next(500);
                var duration = TimeSpan.FromMilliseconds(backoff + noise);
                await Task.Delay(duration).ConfigureAwait(false);

                _logger.LogWarning(
                    "[ServerConnector][{SystemAddress}] Restarting endpoint connection to {Address} after {Duration} because of {Reason} ({Retries} / {MaxRetries})",
                    _system.Address, _address, duration, e.GetType().Name, rs.FailureCount, _maxNrOfRetries);
            }
        }
    }

    private bool ShouldStop(RestartStatistics rs)
    {
        if (_maxNrOfRetries == 0)
        {
            return true;
        }

        rs.Fail();

        if (rs.FailureCount > _maxNrOfRetries)
        {
            rs.Reset();

            return true;
        }

        return false;
    }
}
