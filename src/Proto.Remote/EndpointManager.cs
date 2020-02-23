﻿// -----------------------------------------------------------------------
//   <copyright file="EndpointManager.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Threading;
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

        private static readonly ILogger Logger = Log.CreateLogger(typeof(EndpointManager).FullName);

        private static readonly ConnectionRegistry Connections = new ConnectionRegistry();
        private static PID endpointSupervisor;
        private static Subscription<object> endpointTermEvnSub;
        private static Subscription<object> endpointConnEvnSub;

        public static void Start()
        {
            Logger.LogDebug("Started EndpointManager");

            var props = Props
                .FromProducer(() => new EndpointSupervisor())
                .WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy)
                .WithDispatcher(Mailbox.Dispatchers.SynchronousDispatcher);

            endpointSupervisor = RootContext.Empty.SpawnNamed(props, "EndpointSupervisor");
            endpointTermEvnSub = EventStream.Instance.Subscribe<EndpointTerminatedEvent>(OnEndpointTerminated);
            endpointConnEvnSub = EventStream.Instance.Subscribe<EndpointConnectedEvent>(OnEndpointConnected);
        }

        public static void Stop()
        {
            EventStream.Instance.Unsubscribe(endpointTermEvnSub.Id);
            EventStream.Instance.Unsubscribe(endpointConnEvnSub.Id);

            Connections.Clear();
            RootContext.Empty.Stop(endpointSupervisor);
            Logger.LogDebug("Stopped EndpointManager");
        }

        private static void OnEndpointTerminated(EndpointTerminatedEvent msg)
        {
            Logger.LogDebug("Endpoint {Address} terminated removing from connections", msg.Address);

            if (!Connections.TryRemove(msg.Address, out var v)) return;

            var endpoint = v.Value;
            RootContext.Empty.Send(endpoint.Watcher, msg);
            RootContext.Empty.Send(endpoint.Writer, msg);
        }

        private static void OnEndpointConnected(EndpointConnectedEvent msg)
        {
            var endpoint = EnsureConnected(msg.Address);
            RootContext.Empty.Send(endpoint.Watcher, msg);
            endpoint.Writer.SendSystemMessage(msg);
        }

        public static void RemoteTerminate(RemoteTerminate msg)
        {
            var endpoint = EnsureConnected(msg.Watchee.Address);
            RootContext.Empty.Send(endpoint.Watcher, msg);
        }

        public static void RemoteWatch(RemoteWatch msg)
        {
            var endpoint = EnsureConnected(msg.Watchee.Address);
            RootContext.Empty.Send(endpoint.Watcher, msg);
        }

        public static void RemoteUnwatch(RemoteUnwatch msg)
        {
            var endpoint = EnsureConnected(msg.Watchee.Address);
            RootContext.Empty.Send(endpoint.Watcher, msg);
        }

        public static void RemoteDeliver(RemoteDeliver msg)
        {
            var endpoint = EnsureConnected(msg.Target.Address);

            Logger.LogDebug(
                "Forwarding message for {Address} through EndpontWriter {Writer}",
                msg.Target.Address, endpoint.Writer
            );
            RootContext.Empty.Send(endpoint.Writer, msg);
        }

        private static Endpoint EnsureConnected(string address)
        {
            var conn = Connections.GetOrAdd(
                address, v =>
                    new Lazy<Endpoint>(
                        () =>
                        {
                            Logger.LogDebug("Requesting new endpoint for {Address}", v);

                            var endpoint = RootContext.Empty.RequestAsync<Endpoint>(endpointSupervisor, v).Result;

                            Logger.LogDebug("Created new endpoint for {Address}", v);

                            return endpoint;
                        }
                    )
            );
            return conn.Value;
        }
    }

    public class EndpointSupervisor : IActor, ISupervisorStrategy
    {
        private static readonly ILogger Logger = Log.CreateLogger<EndpointSupervisor>();

        private readonly int _maxNrOfRetries;
        private readonly Random _random = new Random();
        private readonly TimeSpan? _withinTimeSpan;
        private CancellationTokenSource _cancelFutureRetries;

        private int _backoff;
        private string _address;

        public EndpointSupervisor()
        {
            _maxNrOfRetries = Remote.RemoteConfig.EndpointWriterOptions.MaxRetries;
            _withinTimeSpan = Remote.RemoteConfig.EndpointWriterOptions.RetryTimeSpan;
            _backoff = Remote.RemoteConfig.EndpointWriterOptions.RetryBackOffms;
        }

        public Task ReceiveAsync(IContext context)
        {
            if (context.Message is string address)
            {
                _address = address;
                var watcher = SpawnWatcher(address, context);
                var writer = SpawnWriter(address, context);
                _cancelFutureRetries = new CancellationTokenSource();
                context.Respond(new Endpoint(writer, watcher));
            }

            return Actor.Done;
        }

        public void HandleFailure(
            ISupervisor supervisor, PID child, RestartStatistics rs, Exception reason,
            object message
        )
        {
            if (ShouldStop(rs))
            {
                Logger.LogWarning(
                    "Stopping connection to address {Address} after retries expired because of {Reason}",
                    _address, reason
                );

                _cancelFutureRetries.Cancel();
                supervisor.StopChildren(child);
                ProcessRegistry.Instance.Remove(child); //TODO: work out why this hangs around in the process registry

                var terminated = new EndpointTerminatedEvent {Address = _address};
                Actor.EventStream.Publish(terminated);
            }
            else
            {
                _backoff *= 2;
                var noise = _random.Next(_backoff);
                var duration = TimeSpan.FromMilliseconds(_backoff + noise);

                Task.Delay(duration)
                    .ContinueWith(
                        t =>
                        {
                            Logger.LogWarning(
                                "Restarting {Actor} after {Duration} because of {Reason}",
                                child.ToShortString(), duration, reason
                            );
                            supervisor.RestartChildren(reason, child);
                        }, _cancelFutureRetries.Token
                    );
            }
        }

        private bool ShouldStop(RestartStatistics rs)
        {
            if (_maxNrOfRetries == 0)
            {
                return true;
            }

            rs.Fail();

            if (rs.NumberOfFailures(_withinTimeSpan) > _maxNrOfRetries)
            {
                rs.Reset();
                return true;
            }

            return false;
        }

        private static PID SpawnWatcher(string address, IContext context)
        {
            var watcherProps = Props.FromProducer(() => new EndpointWatcher(address));
            var watcher = context.Spawn(watcherProps);
            return watcher;
        }

        private static PID SpawnWriter(string address, IContext context)
        {
            var writerProps =
                Props.FromProducer(
                        () => new EndpointWriter(
                            address, Remote.RemoteConfig.ChannelOptions, Remote.RemoteConfig.CallOptions, Remote.RemoteConfig.ChannelCredentials
                        )
                    )
                    .WithMailbox(() => new EndpointWriterMailbox(Remote.RemoteConfig.EndpointWriterOptions.EndpointWriterBatchSize));
            var writer = context.Spawn(writerProps);
            return writer;
        }
    }
}