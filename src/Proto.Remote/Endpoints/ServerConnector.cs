// -----------------------------------------------------------------------
//   <copyright file="ServerConnector.cs" company="Asynkron AB">
//       Copyright (C) 2015-2025 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly CancellationTokenSource _cts = new();
    private readonly IEndpoint _endpoint;
    private readonly ILogger _logger = Log.CreateLogger<ServerConnector>();
    private readonly KeyValuePair<string, object?>[] _metricTags = Array.Empty<KeyValuePair<string, object?>>();
    private readonly Task _runner;
    private readonly ActorSystem _system;

    public ServerConnector(string address, Type connectorType, IEndpoint endpoint,
        ActorSystem system, RemoteConfig remoteConfig, RemoteMessageHandler remoteMessageHandler)
    {
        _system = system;
        _address = address;
        _endpoint = endpoint;

        if (_system.Metrics.Enabled)
        {
            _metricTags = new KeyValuePair<string, object?>[]
            {
                new("id", _system.Id),
                new("address", _system.Address)
            };
        }

        IConnectionMode mode = connectorType switch
        {
            Type.ServerSide => new ServerConnectionMode((msg, addr) =>
                _logger.LogWarning("[ServerConnector][{SystemAddress}] Received {MessagePayload} from {Address}",
                    _system.Address, msg, addr)),
            Type.ClientSide => new ClientConnectionMode(remoteMessageHandler),
            _ => throw new ArgumentOutOfRangeException(nameof(connectorType), connectorType, null)
        };

        Action onConnected = _system.Metrics.Enabled
            ? () => RemoteMetrics.RemoteEndpointConnectedCount.Add(1,
                new KeyValuePair<string, object?>("id", _system.Id),
                new KeyValuePair<string, object?>("address", _system.Address),
                new KeyValuePair<string, object?>("destinationaddress", _address))
            : () => { };

        Action onDisconnected = _system.Metrics.Enabled
            ? () => RemoteMetrics.RemoteEndpointDisconnectedCount.Add(1, _metricTags)
            : () => { };

        Action<double> recordWrite = _system.Metrics.Enabled
            ? s => RemoteMetrics.RemoteWriteDuration.Record(s, _metricTags)
            : _ => { };

        var runner = new ConnectionRunner(_address, _endpoint, _system, remoteConfig, mode,
            remoteConfig.EndpointWriterOptions.RetryBackOff,
            remoteConfig.EndpointWriterOptions.MaxRetries,
            new Random(),
            _cts.Token,
            onConnected,
            onDisconnected,
            recordWrite,
            msg => _logger.LogInformation(msg),
            msg => _logger.LogDebug(msg),
            msg => _logger.LogWarning(msg),
            (ex, msg) => _logger.LogError(ex, msg));

        _runner = Task.Run(runner.RunAsync);
    }

    public async Task Stop()
    {
        _cts.Cancel();
        await _runner.ConfigureAwait(false);
    }
}
