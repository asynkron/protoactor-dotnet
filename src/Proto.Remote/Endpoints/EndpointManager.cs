// -----------------------------------------------------------------------
//   <copyright file="EndpointManager.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private readonly ConcurrentDictionary<string, PID> _connections = new();
        private readonly EventStreamSubscription<object> _deadLetterEvnSub;
        private readonly EventStreamSubscription<object>? _endpointConnectedEvnSub;
        private readonly EventStreamSubscription<object> _endpointErrorEvnSub;
        private readonly EventStreamSubscription<object>? _endpointTerminatedEvnSub;
        private readonly RemoteConfigBase _remoteConfig;
        private readonly object _synLock = new();
        private readonly ActorSystem _system;
        private readonly ConcurrentDictionary<string, PID> _terminatedConnections = new();

        public EndpointManager(ActorSystem system, RemoteConfigBase remoteConfig, IChannelProvider channelProvider)
        {
            _system = system;
            _system.ProcessRegistry.RegisterHostResolver(pid => new RemoteProcess(_system, this, pid));
            _remoteConfig = remoteConfig;
            _channelProvider = channelProvider;
            _endpointTerminatedEvnSub = _system.EventStream.Subscribe<EndpointTerminatedEvent>(OnEndpointTerminated, Dispatchers.DefaultDispatcher);
            _endpointConnectedEvnSub = _system.EventStream.Subscribe<EndpointConnectedEvent>(OnEndpointConnected);
            _endpointErrorEvnSub = _system.EventStream.Subscribe<EndpointErrorEvent>(OnEndpointError);
            _deadLetterEvnSub = _system.EventStream.Subscribe<DeadLetterEvent>(OnDeadLetterEvent);
        }

        public CancellationToken CancellationToken => _cancellationTokenSource.Token;
        public PID? ActivatorPid { get; private set; }

        private void OnDeadLetterEvent(DeadLetterEvent deadLetterEvent)
        {
            switch (deadLetterEvent.Message)
            {
                case RemoteWatch msg:
                    msg.Watcher.SendSystemMessage(_system, new Terminated
                        {
                            Why = TerminatedReason.AddressTerminated,
                            Who = msg.Watchee
                        }
                    );
                    break;
                case RemoteDeliver rd:
                    if (rd.Sender != null)
                        _system.Root.Send(rd.Sender, new DeadLetterResponse {Target = rd.Target});
                    _system.EventStream.Publish(new DeadLetterEvent(rd.Target, rd.Message, rd.Sender));
                    break;
            }
        }

        public void Start() => SpawnActivator();

        public void Stop()
        {
            lock (_synLock)
            {
                if (CancellationToken.IsCancellationRequested) return;

                Logger.LogDebug("[EndpointManager] Stopping");

                _system.EventStream.Unsubscribe(_endpointTerminatedEvnSub);
                _system.EventStream.Unsubscribe(_endpointConnectedEvnSub);
                _system.EventStream.Unsubscribe(_endpointErrorEvnSub);
                _system.EventStream.Unsubscribe(_deadLetterEvnSub);

                var stopEndpointTasks = new List<Task>();

                foreach (var endpoint in _connections.Values)
                {
                    stopEndpointTasks.Add(_system.Root.StopAsync(endpoint));
                }

                Task.WhenAll(stopEndpointTasks).GetAwaiter().GetResult();

                _cancellationTokenSource.Cancel();

                _connections.Clear();

                StopActivator();

                Logger.LogDebug("[EndpointManager] Stopped");
            }
        }

        private void OnEndpointError(EndpointErrorEvent evt)
        {
            if (_connections.TryGetValue(evt.Address, out var endpoint))
                endpoint.SendSystemMessage(_system, evt);
        }

        private void OnEndpointTerminated(EndpointTerminatedEvent evt)
        {
            Logger.LogDebug("[EndpointManager] Endpoint {Address} terminated removing from connections", evt.Address);
            
            if (_connections.TryRemove(evt.Address, out var endpoint))
            {
                // ReSharper disable once InconsistentlySynchronizedField
                _system.Root.StopAsync(endpoint).ContinueWith(async _ => {
                        if (_remoteConfig.WaitAfterEndpointTerminationTimeSpan.HasValue && _terminatedConnections.TryAdd(evt.Address, endpoint))
                        {
                            await Task.Delay(_remoteConfig.WaitAfterEndpointTerminationTimeSpan.Value, CancellationToken);
                            _terminatedConnections.TryRemove(evt.Address, out var _);
                        }
                    }, CancellationToken
                );
            }
        }

        private void OnEndpointConnected(EndpointConnectedEvent evt)
        {
            var endpoint = GetEndpoint(evt.Address);
            if (endpoint is null)
                return;

            endpoint.SendSystemMessage(_system, evt);
        }

        internal PID? GetEndpoint(string address)
        {

                if (string.IsNullOrWhiteSpace(address)) throw new ArgumentNullException(nameof(address));

                if (_terminatedConnections.ContainsKey(address) || _cancellationTokenSource.IsCancellationRequested) return null;

                //default to try to fetch from the concurrent dict
                // ReSharper disable once InconsistentlySynchronizedField
                if (_connections.TryGetValue(address, out var pid))
                {
                    return pid;
                }

                lock (_synLock)
                {
                    //this thread previously found no instance, check again, now within the lock
                    // ReSharper disable once InconsistentlySynchronizedField
                    if (_connections.TryGetValue(address, out pid))
                    {
                        return pid;
                    }
                    
                    //still no instance, we can spawn and add it here.
                    Logger.LogDebug("[EndpointManager] Requesting new endpoint for {Address}", address);
                    var props = Props
                        .FromProducer(() => new EndpointActor(address, _remoteConfig, _channelProvider))
                        .WithMailbox(() => new EndpointWriterMailbox(_system, _remoteConfig.EndpointWriterOptions.EndpointWriterBatchSize, address))
                        .WithGuardianSupervisorStrategy(new EndpointSupervisorStrategy(address, _remoteConfig, _system));
                    pid = _system.Root.SpawnNamed(props, $"endpoint-{address}");
                    Logger.LogDebug("[EndpointManager] Created new endpoint for {Address}", address);


                    if (!_connections.TryAdd(address, pid))
                    {
                        //Famous last words, but this should never happen. if it does, someone added this entry outside of this lock
                        Logger.LogWarning("[EndpointManager] Could not add the endpoint {Address}", address);
                    }
                    return pid;
                }
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