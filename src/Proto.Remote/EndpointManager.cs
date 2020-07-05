// -----------------------------------------------------------------------
//   <copyright file="EndpointManager.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Proto.Remote
{
    public class Endpoint
    {
        public Endpoint(PID writer, PID watcher)
        {
            Writer = writer;
            Watcher = watcher;
        }

        public PID Writer { get; }
        public PID Watcher { get; }
    }

    public class EndpointManager
    {
        private class ConnectionRegistry : ConcurrentDictionary<string, Lazy<Endpoint>> { }

        private static readonly ILogger Logger = Log.CreateLogger(typeof(EndpointManager).FullName);

        private readonly ConnectionRegistry _connections = new ConnectionRegistry();
        private readonly ActorSystem _system;
        private readonly Remote _remote;
        private PID? _endpointSupervisor;
        private Subscription<object>? _endpointTermEvnSub;
        private Subscription<object>? _endpointConnEvnSub;

        public EndpointManager(Remote remote, ActorSystem system)
        {
            _remote = remote;
            _system = system;
        }

        public void Start()
        {
            Logger.LogDebug("[EndpointManager] Started");

            var props = Props
                .FromProducer(() => new EndpointSupervisor(_remote, _system))
                .WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy)
                .WithDispatcher(Mailbox.Dispatchers.SynchronousDispatcher);

            _endpointSupervisor = _system.Root.SpawnNamed(props, "EndpointSupervisor");
            _endpointTermEvnSub = _system.EventStream.Subscribe<EndpointTerminatedEvent>(OnEndpointTerminated);
            _endpointConnEvnSub = _system.EventStream.Subscribe<EndpointConnectedEvent>(OnEndpointConnected);
        }

        public void Stop()
        {
            _system.EventStream.Unsubscribe(_endpointTermEvnSub);
            _system.EventStream.Unsubscribe(_endpointConnEvnSub);

            _connections.Clear();
            _system.Root.Stop(_endpointSupervisor);
            Logger.LogDebug("[EndpointManager] Stopped");
        }

        private void OnEndpointTerminated(EndpointTerminatedEvent msg)
        {
            Logger.LogDebug("[EndpointManager] Endpoint {Address} terminated removing from connections", msg.Address);

            if (!_connections.TryRemove(msg.Address, out var v)) return;

            var endpoint = v.Value;
            _system.Root.Send(endpoint.Watcher, msg);
            _system.Root.Send(endpoint.Writer, msg);
        }

        private void OnEndpointConnected(EndpointConnectedEvent msg)
        {
            var endpoint = EnsureConnected(msg.Address);
            _system.Root.Send(endpoint.Watcher, msg);
            endpoint.Writer.SendSystemMessage(_system, msg);
        }

        public void RemoteTerminate(RemoteTerminate msg)
        {
            var endpoint = EnsureConnected(msg.Watchee.Address);
            _system.Root.Send(endpoint.Watcher, msg);
        }

        public void RemoteWatch(RemoteWatch msg)
        {
            var endpoint = EnsureConnected(msg.Watchee.Address);
            _system.Root.Send(endpoint.Watcher, msg);
        }

        public void RemoteUnwatch(RemoteUnwatch msg)
        {
            var endpoint = EnsureConnected(msg.Watchee.Address);
            _system.Root.Send(endpoint.Watcher, msg);
        }

        public void RemoteDeliver(RemoteDeliver msg)
        {
            var endpoint = EnsureConnected(msg.Target.Address);

            Logger.LogDebug(
                "[EndpointManager] Forwarding message {Message} from {From} for {Address} through EndpointWriter {Writer}",
                msg.Message?.GetType(), msg.Sender?.Address, msg.Target?.Address, endpoint.Writer
            );
            _system.Root.Send(endpoint.Writer, msg);
        }

        private Endpoint EnsureConnected(string address)
        {
            var conn = _connections.GetOrAdd(
                address, v =>
                    new Lazy<Endpoint>(
                        () =>
                        {
                            Logger.LogDebug("[EndpointManager] Requesting new endpoint for {Address}", v);

                            var endpoint = _system.Root.RequestAsync<Endpoint>(_endpointSupervisor!, v).Result;

                            Logger.LogDebug("[EndpointManager] Created new endpoint for {Address}", v);

                            return endpoint;
                        }
                    )
            );
            return conn.Value;
        }
    }
}