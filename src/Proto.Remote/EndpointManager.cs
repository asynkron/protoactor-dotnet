// -----------------------------------------------------------------------
//   <copyright file="EndpointManager.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
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

        private static readonly ILogger Logger = Log.CreateLogger("EndpointManager");

        private static readonly ConnectionRegistry Connections = new ConnectionRegistry();
        private static PID _endpointSupervisor;
        private static Subscription<object> _endpointTermEvnSub;
        private static Subscription<object> _endpointConnEvnSub;

        public static void Start()
        {
            Logger.LogDebug("Started EndpointManager");

            var props = Props.FromProducer(() => new EndpointSupervisor())
                             .WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy)
                             .WithDispatcher(Mailbox.Dispatchers.SynchronousDispatcher);
            _endpointSupervisor = RootContext.Empty.SpawnNamed(props, "EndpointSupervisor");
            _endpointTermEvnSub = EventStream.Instance.Subscribe<EndpointTerminatedEvent>(OnEndpointTerminated);
            _endpointConnEvnSub = EventStream.Instance.Subscribe<EndpointConnectedEvent>(OnEndpointConnected);
        }

        public static void Stop()
        {
            EventStream.Instance.Unsubscribe(_endpointTermEvnSub.Id);
            EventStream.Instance.Unsubscribe(_endpointConnEvnSub.Id);

            Connections.Clear();
            _endpointSupervisor.Stop();
            Logger.LogDebug("Stopped EndpointManager");
        }

        private static void OnEndpointTerminated(EndpointTerminatedEvent msg)
        {
            if (Connections.TryRemove(msg.Address, out var v))
            {
                var endpoint = v.Value;
                RootContext.Empty.Send(endpoint.Watcher,msg);
                RootContext.Empty.Send(endpoint.Writer,msg);
            }
        }

        private static void OnEndpointConnected(EndpointConnectedEvent msg)
        {
            var endpoint = EnsureConnected(msg.Address);
            RootContext.Empty.Send(endpoint.Watcher,msg);
        }

        public static void RemoteTerminate(RemoteTerminate msg)
        {
            var endpoint = EnsureConnected(msg.Watchee.Address);
            RootContext.Empty.Send(endpoint.Watcher,msg);
        }

        public static void RemoteWatch(RemoteWatch msg)
        {
            var endpoint = EnsureConnected(msg.Watchee.Address);
            RootContext.Empty.Send(endpoint.Watcher,msg);
        }

        public static void RemoteUnwatch(RemoteUnwatch msg)
        {
            var endpoint = EnsureConnected(msg.Watchee.Address);
            RootContext.Empty.Send(endpoint.Watcher,msg);
        }

        public static void RemoteDeliver(RemoteDeliver msg)
        {
            var endpoint = EnsureConnected(msg.Target.Address);
            RootContext.Empty.Send(endpoint.Writer, msg);
        }

        private static Endpoint EnsureConnected(string address)
        {
            var conn = Connections.GetOrAdd(address, v =>
                new Lazy<Endpoint>(() =>
                    RootContext.Empty.RequestAsync<Endpoint>(_endpointSupervisor, v).Result)
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
            var watcherProps = Props.FromProducer(() => new EndpointWatcher(address));
            var watcher = context.Spawn(watcherProps);
            return watcher;
        }

        private PID SpawnWriter(string address, IContext context)
        {
            var writerProps =
                Props.FromProducer(() => new EndpointWriter(address, Remote.RemoteConfig.ChannelOptions, Remote.RemoteConfig.CallOptions, Remote.RemoteConfig.ChannelCredentials))
                     .WithMailbox(() => new EndpointWriterMailbox(Remote.RemoteConfig.EndpointWriterBatchSize));
            var writer = context.Spawn(writerProps);
            return writer;
        }
    }
}
