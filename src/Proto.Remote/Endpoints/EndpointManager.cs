// -----------------------------------------------------------------------
//   <copyright file="EndpointManager.cs" company="Asynkron AB">
//       Copyright (C) 2015-2022 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Mailbox;

namespace Proto.Remote
{
    public class EndpointManager
    {
        private static readonly ILogger Logger = Log.CreateLogger<EndpointManager>();
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly IChannelProvider _channelProvider;
        private readonly ConcurrentDictionary<string, IEndpoint> _serverEndpoints = new();
        private readonly ConcurrentDictionary<string, IEndpoint> _clientEndpoints = new();
        private readonly ConcurrentDictionary<string, DateTime> _blockedAddresses = new();
        private readonly ConcurrentDictionary<string, DateTime> _blockedClientSystemIds = new();
        private readonly EventStreamSubscription<object>? _endpointTerminatedEvnSub;
        private readonly RemoteConfigBase _remoteConfig;
        private readonly object _synLock = new();
        private readonly ActorSystem _system;
        private readonly IEndpoint _blockedEndpoint;
        internal RemoteMessageHandler RemoteMessageHandler { get; }

        public EndpointManager(ActorSystem system, RemoteConfigBase remoteConfig, IChannelProvider channelProvider)
        {
            _system = system;
            _system.ProcessRegistry.RegisterHostResolver(pid => new RemoteProcess(_system, this, pid));
            _remoteConfig = remoteConfig;
            _channelProvider = channelProvider;
            _endpointTerminatedEvnSub = _system.EventStream.Subscribe<EndpointTerminatedEvent>(OnEndpointTerminated, Dispatchers.DefaultDispatcher);
            _blockedEndpoint = new BlockedEndpoint(system);
            RemoteMessageHandler = new RemoteMessageHandler(this, _system, _remoteConfig.Serialization, _remoteConfig);
        }

        public CancellationToken CancellationToken => _cancellationTokenSource.Token;
        private PID? ActivatorPid { get; set; }
        public void Start() => SpawnActivator();
        public void Stop()
        {
            lock (_synLock)
            {
                if (CancellationToken.IsCancellationRequested) return;

                Logger.LogDebug("[{SystemAddress}] Stopping", _system.Address);

                _system.EventStream.Unsubscribe(_endpointTerminatedEvnSub);

                _cancellationTokenSource.Cancel();

                foreach (var endpoint in _serverEndpoints.Values)
                {
                    endpoint.DisposeAsync().GetAwaiter().GetResult();
                }

                foreach (var endpoint in _clientEndpoints.Values)
                {
                    endpoint.DisposeAsync().GetAwaiter().GetResult();
                }

                _serverEndpoints.Clear();
                _clientEndpoints.Clear();

                StopActivator();

                Logger.LogDebug("[{SystemAddress}] Stopped", _system.Address);
            }
        }
        private void OnEndpointTerminated(EndpointTerminatedEvent evt)
        {
            if(Logger.IsEnabled(LogLevel.Debug))
                Logger.LogDebug("[{SystemAddress}] Endpoint {Address} terminating", _system.Address, evt.Address ?? evt.ActorSystemId);
            lock (_synLock)
            {
                if (evt.Address is not null && _serverEndpoints.TryRemove(evt.Address, out var endpoint))
                {
                    endpoint.DisposeAsync().GetAwaiter().GetResult();

                    if (evt.OnError && _remoteConfig.WaitAfterEndpointTerminationTimeSpan.HasValue && _blockedAddresses.TryAdd(evt.Address, DateTime.UtcNow))
                    {
                        _ = SafeTask.Run(async () => {
                            await Task.Delay(_remoteConfig.WaitAfterEndpointTerminationTimeSpan.Value).ConfigureAwait(false);
                            _blockedAddresses.TryRemove(evt.Address, out var _);
                        });
                    }
                }
                if (evt.ActorSystemId is not null && _clientEndpoints.TryRemove(evt.ActorSystemId, out endpoint))
                {
                    endpoint.DisposeAsync().GetAwaiter().GetResult();

                    if (evt.OnError && _remoteConfig.WaitAfterEndpointTerminationTimeSpan.HasValue && _blockedClientSystemIds.TryAdd(evt.ActorSystemId, DateTime.UtcNow))
                    {
                        _ = SafeTask.Run(async () => {
                            await Task.Delay(_remoteConfig.WaitAfterEndpointTerminationTimeSpan.Value).ConfigureAwait(false);
                            _blockedClientSystemIds.TryRemove(evt.ActorSystemId, out var _);
                        });
                    }
                }
            }
            Logger.LogDebug("[{SystemAddress}] Endpoint {Address} terminated", _system.Address, evt.Address ?? evt.ActorSystemId);
        }
        internal IEndpoint GetOrAddServerEndpoint(string address)
        {
            if (address is null)
            {
                Logger.LogError("[{SystemAddress}] Tried to get endpoint for null address",_system.Address);
                return _blockedEndpoint;
            }
            
            if (_cancellationTokenSource.IsCancellationRequested || _blockedAddresses.ContainsKey(address))
                return _blockedEndpoint;

            if (_serverEndpoints.TryGetValue(address, out var endpoint))
            {
                return endpoint;
            }

            lock (_synLock)
            {
                if (_serverEndpoints.TryGetValue(address, out endpoint))
                {
                    return endpoint;
                }

                if (_system.Address.StartsWith(ActorSystem.Client, StringComparison.Ordinal))
                {
                    Logger.LogDebug("[{SystemAddress}] Requesting new client side ServerEndpoint for {Address}", _system.Address, address);
                    endpoint = _serverEndpoints.GetOrAdd(address, v => new ServerEndpoint(_system, _remoteConfig, v, _channelProvider, ServerConnector.Type.ClientSide, RemoteMessageHandler));
                }
                else
                {
                    Logger.LogDebug("[{SystemAddress}] Requesting new server side ServerEndpoint for {Address}", _system.Address, address);
                    endpoint = _serverEndpoints.GetOrAdd(address, v => new ServerEndpoint(_system, _remoteConfig, v, _channelProvider, ServerConnector.Type.ServerSide, RemoteMessageHandler));
                }
                return endpoint;
            }
        }
        internal IEndpoint GetOrAddClientEndpoint(string systemId)
        {
            if (systemId is null)
            {
                Logger.LogError("[{SystemAddress}] Tried to get endpoint for null systemId",_system.Address);
                return _blockedEndpoint;
            }
            
            if (_cancellationTokenSource.IsCancellationRequested || _blockedClientSystemIds.ContainsKey(systemId))
                return _blockedEndpoint;

            if (_clientEndpoints.TryGetValue(systemId, out var endpoint))
            {
                return endpoint;
            }

            lock (_synLock)
            {
                if (_clientEndpoints.TryGetValue(systemId, out endpoint))
                {
                    return endpoint;
                }

                Logger.LogDebug("[{SystemAddress}] Requesting new ServerSideClientEndpoint for {SystemId}", _system.Address, systemId);

                return _clientEndpoints.GetOrAdd(systemId, address => new ServerSideClientEndpoint(_system, _remoteConfig, address));

            }
        }
        internal IEndpoint GetServerEndpoint(string address)
        {
            if (_cancellationTokenSource.IsCancellationRequested || _blockedAddresses.ContainsKey(address))
                return _blockedEndpoint;

            if (_serverEndpoints.TryGetValue(address, out var endpoint))
            {
                return endpoint;
            }
            return _blockedEndpoint;
        }
        internal IEndpoint GetClientEndpoint(string systemId)
        {
            if (_cancellationTokenSource.IsCancellationRequested || _blockedClientSystemIds.ContainsKey(systemId))
                return _blockedEndpoint;

            if (_clientEndpoints.TryGetValue(systemId, out var endpoint))
            {
                return endpoint;
            }
            return _blockedEndpoint;
        }
        private void SpawnActivator()
        {
            var props = Props.FromProducer(() => new Activator(_remoteConfig, _system))
                .WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy);
            ActivatorPid = _system.Root.SpawnNamed(props, "activator");
        }
        private void StopActivator() => _system.Root.Stop(ActivatorPid);
    }
}