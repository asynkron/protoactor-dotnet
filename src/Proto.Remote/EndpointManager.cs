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
        private readonly ConcurrentDictionary<string, PID> _connections = new ConcurrentDictionary<string, PID>();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly ActorSystem _system;
        private readonly EventStreamSubscription<object>? _endpointConnectedEvnSub;
        private readonly EventStreamSubscription<object>? _endpointTerminatedEvnSub;
        private readonly EventStreamSubscription<object> _endpointErrorEvnSub;
        private readonly RemoteConfigBase _remoteConfig;
        private readonly IChannelProvider _channelProvider;
        private readonly object _synLock = new object();
        public CancellationToken CancellationToken => _cancellationTokenSource.Token;
        public PID? ActivatorPid { get; private set; }

        public EndpointManager(ActorSystem system, RemoteConfigBase remoteConfig, IChannelProvider channelProvider)
        {
            _system = system;
            _system.ProcessRegistry.RegisterHostResolver(pid => new RemoteProcess(_system, this, pid));
            _remoteConfig = remoteConfig;
            _channelProvider = channelProvider;
            _endpointTerminatedEvnSub = _system.EventStream.Subscribe<EndpointTerminatedEvent>(OnEndpointTerminated, Dispatchers.DefaultDispatcher);
            _endpointConnectedEvnSub = _system.EventStream.Subscribe<EndpointConnectedEvent>(OnEndpointConnected);
            _endpointErrorEvnSub = _system.EventStream.Subscribe<EndpointErrorEvent>(OnEndpointError);
        }

        public void Start()
        {
            SpawnActivator();
        }

        public void Stop()
        {
            lock (_synLock)
            {
                if (CancellationToken.IsCancellationRequested) return;
                Logger.LogDebug("[EndpointManager] Stopping");

                _system.EventStream.Unsubscribe(_endpointTerminatedEvnSub);
                _system.EventStream.Unsubscribe(_endpointConnectedEvnSub);
                _system.EventStream.Unsubscribe(_endpointErrorEvnSub);

                _cancellationTokenSource.Cancel();

                var stopEndpointTasks = new List<Task>();
                foreach (var endpoint in _connections.Values)
                {
                    stopEndpointTasks.Add(_system.Root.StopAsync(endpoint));
                }

                Task.WhenAll(stopEndpointTasks).GetAwaiter().GetResult();

                _connections.Clear();

                StopActivator();

                Logger.LogDebug("[EndpointManager] Stopped");
            }
        }

        private void OnEndpointError(EndpointErrorEvent evt)
        {
            lock (_synLock)
            {
                var endpoint = GetEndpoint(evt.Address);
                endpoint.SendSystemMessage(_system, evt);
            }
        }

        private void OnEndpointTerminated(EndpointTerminatedEvent evt)
        {
            Logger.LogDebug("[EndpointManager] Endpoint {Address} terminated removing from connections", evt.Address);
            lock (_synLock)
            {
                if (_connections.TryRemove(evt.Address, out var endpoint))
                {
                    _system.Root.StopAsync(endpoint).GetAwaiter().GetResult();
                }
            }
        }

        private void OnEndpointConnected(EndpointConnectedEvent evt)
        {
            lock (_synLock)
            {
                var endpoint = GetEndpoint(evt.Address);
                endpoint.SendSystemMessage(_system, evt);
            }
        }

        public void RemoteTerminate(RemoteTerminate msg)
        {
            lock (_synLock)
            {
                var endpoint = GetEndpoint(msg.Watchee.Address);
                _system.Root.Send(endpoint, msg);
            }
        }

        public void RemoteWatch(RemoteWatch msg)
        {
            lock (_synLock)
            {
                var endpoint = GetEndpoint(msg.Watchee.Address);
                _system.Root.Send(endpoint, msg);
            }
        }

        public void RemoteUnwatch(RemoteUnwatch msg)
        {
            lock (_synLock)
            {
                var endpoint = GetEndpoint(msg.Watchee.Address);
                _system.Root.Send(endpoint, msg);
            }
        }

        public void RemoteDeliver(RemoteDeliver msg)
        {
            if (string.IsNullOrWhiteSpace(msg.Target.Address))
                throw new ArgumentOutOfRangeException("Target");
            if (CancellationToken.IsCancellationRequested)
            {
                _system.EventStream.Publish(new DeadLetterEvent(msg.Target, msg.Message, msg.Sender));
                return;
            };
            lock (_synLock)
            {
                var endpoint = GetEndpoint(msg.Target.Address);
                Logger.LogDebug(
                    "[EndpointManager] Forwarding message {Message} from {From} for {Address} through EndpointWriter {Writer}",
                    msg.Message?.GetType(), msg.Sender?.Address, msg.Target?.Address, endpoint
                );
                _system.Root.Send(endpoint, msg);
            }
        }

        private PID GetEndpoint(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentNullException(nameof(address));
            }
            return _connections.GetOrAdd(address, v =>
            {
                Logger.LogDebug("[EndpointManager] Requesting new endpoint for {Address}", v);
                var props = Props
                    .FromProducer(() => new EndpointActor(v, this, _remoteConfig, _channelProvider))
                    .WithMailbox(() => new EndpointWriterMailbox(_system, _remoteConfig.EndpointWriterOptions.EndpointWriterBatchSize, v))
                    .WithGuardianSupervisorStrategy(new EndpointSupervisorStrategy(v, _remoteConfig, _system));
                var endpointActorPid = _system.Root.SpawnNamed(props, $"endpoint-{v}");
                Logger.LogDebug("[EndpointManager] Created new endpoint for {Address}", v);
                return endpointActorPid;
            });
        }

        public void SendMessage(PID pid, object msg, int serializerId)
        {
            var (message, sender, header) = Proto.MessageEnvelope.Unwrap(msg);
            var env = new RemoteDeliver(header!, message, pid, sender!, serializerId);
            RemoteDeliver(env);
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