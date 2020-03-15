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

        private readonly ConnectionRegistry Connections = new ConnectionRegistry();
        private readonly ActorSystem _system;
        private readonly Remote _remote;
        private PID endpointSupervisor;
        private Subscription<object> endpointTermEvnSub;
        private Subscription<object> endpointConnEvnSub;

        public EndpointManager(Remote remote, ActorSystem system)
        {
            _remote = remote;
            _system = system;
        }

        public void Start()
        {
            Logger.LogDebug("Started EndpointManager");

            var props = Props
                .FromProducer(() => new EndpointSupervisor(_remote, _system))
                .WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy)
                .WithDispatcher(Mailbox.Dispatchers.SynchronousDispatcher);

            endpointSupervisor = _system.Root.SpawnNamed(props, "EndpointSupervisor");
            endpointTermEvnSub = _system.EventStream.Subscribe<EndpointTerminatedEvent>(OnEndpointTerminated);
            endpointConnEvnSub = _system.EventStream.Subscribe<EndpointConnectedEvent>(OnEndpointConnected);
        }

        public void Stop()
        {
            _system.EventStream.Unsubscribe(endpointTermEvnSub.Id);
            _system.EventStream.Unsubscribe(endpointConnEvnSub.Id);

            Connections.Clear();
            _system.Root.Stop(endpointSupervisor);
            Logger.LogDebug("Stopped EndpointManager");
        }

        private void OnEndpointTerminated(EndpointTerminatedEvent msg)
        {
            Logger.LogDebug("Endpoint {Address} terminated removing from connections", msg.Address);

            if (!Connections.TryRemove(msg.Address, out var v)) return;

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
                "Forwarding message {Message} from {From} for {Address} through EndpointWriter {Writer}",
                msg.Message?.GetType(), msg.Sender?.Address, msg.Target?.Address, endpoint.Writer
            );
            _system.Root.Send(endpoint.Writer, msg);
        }

        private Endpoint EnsureConnected(string address)
        {
            var conn = Connections.GetOrAdd(
                address, v =>
                    new Lazy<Endpoint>(
                        () =>
                        {
                            Logger.LogDebug("Requesting new endpoint for {Address}", v);

                            var endpoint = _system.Root.RequestAsync<Endpoint>(endpointSupervisor, v).Result;

                            Logger.LogDebug("Created new endpoint for {Address}", v);

                            return endpoint;
                        }
                    )
            );
            return conn.Value;
        }
    }
}