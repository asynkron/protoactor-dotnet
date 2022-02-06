// -----------------------------------------------------------------------
//   <copyright file="ServerConnector.cs" company="Asynkron AB">
//       Copyright (C) 2015-2022 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Proto.Remote.Metrics;

namespace Proto.Remote
{
    public class ServerConnector
    {
        public enum Type
        {
            ServerSide,
            ClientSide
        }

        private readonly ILogger _logger = Log.CreateLogger<ServerConnector>();
        private readonly ActorSystem _system;
        private readonly IChannelProvider _channelProvider;
        private readonly RemoteConfigBase _remoteConfig;
        private readonly RemoteMessageHandler _remoteMessageHandler;
        private readonly string _address;
        private readonly Type _connectorType;
        private readonly IEndpoint _endpoint;
        private readonly TimeSpan _backoff;
        private readonly int _maxNrOfRetries;
        private readonly Random _random = new();
        private readonly TimeSpan? _withinTimeSpan;
        private readonly Task _runner;
        private readonly CancellationTokenSource _cts = new();
        private readonly KeyValuePair<string, object?>[] _metricTags = Array.Empty<KeyValuePair<string, object?>>();
        
        public async Task Stop()
        {
            _cts.Cancel();
            await _runner.ConfigureAwait(false);
        }
        public ServerConnector(string address, Type connectorType, IEndpoint endpoint, IChannelProvider channelProvider, ActorSystem system, RemoteConfigBase remoteConfig, RemoteMessageHandler remoteMessageHandler)
        {
            _channelProvider = channelProvider;
            _system = system;
            _remoteConfig = remoteConfig;
            _remoteMessageHandler = remoteMessageHandler;
            _address = address;
            _connectorType = connectorType;
            _endpoint = endpoint;
            _maxNrOfRetries = remoteConfig.EndpointWriterOptions.MaxRetries;
            _withinTimeSpan = remoteConfig.EndpointWriterOptions.RetryTimeSpan;
            _backoff = remoteConfig.EndpointWriterOptions.RetryBackOff;
            _runner = Task.Run(() => RunAsync());
            if (_system.Metrics.Enabled)
                _metricTags = new KeyValuePair<string, object?>[] {new("id", _system.Id), new("address", _system.Address)};
        }
        public async Task RunAsync()
        {
            string? actorSystemId = null;
            var rs = new RestartStatistics(0, null);
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("[{SystemAddress}] Connecting to {Address}...", _system.Address, _address);
                    var channel = _channelProvider.GetChannel(_address);
                    var client = new Remoting.RemotingClient(channel);
                    using var call = client.Receive(_remoteConfig.CallOptions);

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
                                        SystemId = _system.Id
                                    }
                                }
                            }).ConfigureAwait(false);
                            break;
                        case Type.ClientSide:
                            await call.RequestStream.WriteAsync(new RemoteMessage
                            {
                                ConnectRequest = new ConnectRequest
                                {
                                    ClientConnection = new ClientConnection
                                    {
                                        SystemId = _system.Id
                                    }
                                }
                            }).ConfigureAwait(false);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    await call.ResponseStream.MoveNext().ConfigureAwait(false);
                    var response = call.ResponseStream.Current;
                    if (response.MessageTypeCase != RemoteMessage.MessageTypeOneofCase.ConnectResponse)
                        throw new Exception("Expected ConnectResponse");
                    var connectResponse = response.ConnectResponse;
                    
                    if (connectResponse.Blocked)
                    {
                        _logger.LogError("[{SystemAddress}] Connection Refused to remote member {MemberId} address {Address}, we are blocked", _system.Address, connectResponse.MemberId, _address);
                        var terminated = new EndpointTerminatedEvent(false, _address, _system.Id);
                        _system.EventStream.Publish(terminated);
                        return;
                    }
                    
                    actorSystemId = connectResponse.MemberId;

                    if (_system.Remote().BlockList.IsBlocked(actorSystemId))
                    {
                        _logger.LogError("[{SystemAddress}] Connection Refused to remote member {MemberId} address {Address}, they are blocked", _system.Address, connectResponse.MemberId, _address);
                        var terminated = new EndpointTerminatedEvent(false, _address, _system.Id);
                        _system.EventStream.Publish(terminated);
                        return;
                    }

                    rs.Reset();
                    var cancellationTokenSource = new CancellationTokenSource();
                    var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationTokenSource.Token).Token;

                    var writer = Task.Run(async () => {
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
                                await foreach (var message in _endpoint.Outgoing.Reader.ReadAllAsync(combinedToken).ConfigureAwait(false))
                                {
                                    try
                                    {
                                        // _logger.LogInformation($"Sending {message}");
                                        await call.RequestStream.WriteAsync(message).ConfigureAwait(false);
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
                                _logger.LogDebug("[{SystemAddress}] Writer cancelled for {Address}", _system.Address, _address);
                            }
                        }
                    });
                    var reader = Task.Run(async () => {
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
                                            _logger.LogDebug("[{SystemAddress}] Received disconnection request from {Address}", _system.Address, _address);
                                            var terminated = new EndpointTerminatedEvent(false, _address, actorSystemId);
                                            _system.EventStream.Publish(terminated);
                                            break;
                                        }
                                    default:
                                        if (_connectorType == Type.ServerSide)
                                            _logger.LogWarning("[{SystemAddress}] Received {Message} from {_address}", _system.Address, currentMessage, _address);
                                        else
                                            _remoteMessageHandler.HandleRemoteMessage(currentMessage);
                                        break;
                                }
                            }
                            _logger.LogDebug("[{SystemAddress}] Reader finished for {Address}", _system.Address, _address);
                        }
                        catch (OperationCanceledException)
                        {
                            _logger.LogDebug("[{SystemAddress}] Reader cancelled for {Address}", _system.Address, _address);
                        }
                        catch (RpcException e) when (e.StatusCode == StatusCode.Cancelled)
                        {
                            _logger.LogWarning("[{SystemAddress}] Reader cancelled for {Address}", _system.Address, _address);
                        }
                        catch (Exception e)
                        {
                            _logger.LogWarning("[{SystemAddress}] Error in reader for {Address} {Reason}", _system.Address, _address, e.GetType().Name);
                            cancellationTokenSource.Cancel();
                            throw;
                        }
                    });
                    _logger.LogInformation("[{SystemAddress}] Connected to {Address}", _system.Address, _address);
                    await writer.ConfigureAwait(false);
                    cancellationTokenSource.Cancel();
                    await call.RequestStream.CompleteAsync().ConfigureAwait(false);
                    await reader.ConfigureAwait(false);
                    
                    if (_system.Metrics.Enabled)
                        RemoteMetrics.RemoteEndpointDisconnectedCount.Add(1, _metricTags);

                    _logger.LogInformation("[{SystemAddress}] Disconnected from {Address}", _system.Address, _address);
                }
                catch (Exception e)
                {
                    if (actorSystemId is not null && _system.Remote().BlockList.IsBlocked(actorSystemId))
                    {
                        _logger.LogDebug("[{SystemAddress}] dropped connection to blocked member {ActorSystemId}/{Address}", _system.Address, actorSystemId, _address);
                        var terminated = new EndpointTerminatedEvent(true, _address, actorSystemId);
                        _system.EventStream.Publish(terminated);
                        break;
                    }
                    if (ShouldStop(rs))
                    {
                        _logger.LogError(e,"[{SystemAddress}] Stopping connection to {Address} after retries expired because of {Reason}", _system.Address, _address, e.GetType().Name);
                        var terminated = new EndpointTerminatedEvent(true, _address, actorSystemId);
                        _system.EventStream.Publish(terminated);
                        break;
                    }
                    else
                    {
                        var backoff = rs.FailureCount * (int) _backoff.TotalMilliseconds;
                        var noise = _random.Next(500);
                        var duration = TimeSpan.FromMilliseconds(backoff + noise);
                        await Task.Delay(duration).ConfigureAwait(false);
                        _logger.LogWarning("[{SystemAddress}] Restarting endpoint connection to {Address} after {Duration} because of {Reason} ({Retries} / {MaxRetries})", _system.Address, _address, duration, e.GetType().Name, rs.FailureCount, _maxNrOfRetries);
                    }
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
}