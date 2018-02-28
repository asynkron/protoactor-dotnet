// -----------------------------------------------------------------------
//   <copyright file="EndpointManager.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
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

    public static class EndpointManager
    {
        private class ConnectionRegistry : ConcurrentDictionary<string, Lazy<Endpoint>> { }

        private static readonly ILogger _logger = Log.CreateLogger("EndpointManager");

        private static readonly ConnectionRegistry _connections = new ConnectionRegistry();
        private static PID _endpointSupervisor;
        private static Subscription<object> _endpointTermEvnSub;
        private static Subscription<object> _endpointConnEvnSub;

        public static void Start()
        {
            _logger.LogDebug("Started EndpointManager");

            var props = Actor.FromProducer(() => new EndpointSupervisor())
                             .WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy)
                             .WithDispatcher(Proto.Mailbox.Dispatchers.SynchronousDispatcher);
            _endpointSupervisor = Actor.SpawnNamed(props, "EndpointSupervisor");
            _endpointTermEvnSub = EventStream.Instance.Subscribe<EndpointTerminatedEvent>(OnEndpointTerminated);
            _endpointConnEvnSub = EventStream.Instance.Subscribe<EndpointConnectedEvent>(OnEndpointConnected);
        }

        public static void Stop()
        {
            EventStream.Instance.Unsubscribe(_endpointTermEvnSub.Id);
            EventStream.Instance.Unsubscribe(_endpointConnEvnSub.Id);

            _connections.Clear();
            _endpointSupervisor.Stop();
            _logger.LogDebug("Stopped EndpointManager");
        }

        private static void OnEndpointTerminated(EndpointTerminatedEvent msg)
        {
            if (_connections.TryRemove(msg.Address, out var v))
            {
                var endpoint = v.Value;
                endpoint.Watcher.Tell(msg);
                endpoint.Writer.Tell(msg);
            }
        }

        private static void OnEndpointConnected(EndpointConnectedEvent msg)
        {
            var endpoint = EnsureConnected(msg.Address);
            endpoint.Watcher.Tell(msg);
        }

        public static void RemoteTerminate(RemoteTerminate msg)
        {
            var endpoint = EnsureConnected(msg.Watchee.Address);
            endpoint.Watcher.Tell(msg);
        }

        public static void RemoteWatch(RemoteWatch msg)
        {
            var endpoint = EnsureConnected(msg.Watchee.Address);
            endpoint.Watcher.Tell(msg);
        }

        public static void RemoteUnwatch(RemoteUnwatch msg)
        {
            var endpoint = EnsureConnected(msg.Watchee.Address);
            endpoint.Watcher.Tell(msg);
        }

        public static void RemoteDeliver(RemoteDeliver msg)
        {
            var endpoint = EnsureConnected(msg.Target.Address);
            endpoint.Writer.Tell(msg);
        }

        private static Endpoint EnsureConnected(string address)
        {
            var conn = _connections.GetOrAdd(address, v => 
                new Lazy<Endpoint>(() => _endpointSupervisor.RequestAsync<Endpoint>(v).Result)
            );
            return conn.Value;
        }
    }

    public class EndpointSupervisor : IActor, ISupervisorStrategy
    {
        public Task ReceiveAsync(IContext context)
        {
            if (context.Message is string address)
            {
                var watcher = SpawnWatcher(address, context);
                var writer = SpawnWriter(address, context);
                context.Respond(new Endpoint(writer, watcher));
            }
            return Actor.Done;
        }

        public void HandleFailure(ISupervisor supervisor, PID child, RestartStatistics rs, Exception cause)
        {
            supervisor.RestartChildren(cause, child);
        }

        private static PID SpawnWatcher(string address, IContext context)
        {
            var watcherProps = Actor.FromProducer(() => new EndpointWatcher(address));
            var watcher = context.Spawn(watcherProps);
            return watcher;
        }

        private PID SpawnWriter(string address, IContext context)
        {
            var writerProps =
                Actor.FromProducer(() => new EndpointWriter(address, Remote.RemoteConfig.ChannelOptions, Remote.RemoteConfig.CallOptions, Remote.RemoteConfig.ChannelCredentials))
                     .WithMailbox(() => new EndpointWriterMailbox(Remote.RemoteConfig.EndpointWriterBatchSize));
            var writer = context.Spawn(writerProps);
            return writer;
        }
    }
}
