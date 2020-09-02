// -----------------------------------------------------------------------
//   <copyright file="EndpointManager.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto.Remote
{
    public class EndpointSupervisor : IActor, ISupervisorStrategy
    {
        private static readonly ILogger Logger = Log.CreateLogger<EndpointSupervisor>();
        private readonly long _backoff;

        private readonly int _maxNrOfRetries;
        private readonly Random _random = new Random();
        private readonly EndpointManager _endpointManager;
        private readonly RemoteConfig _remoteConfig;
        private readonly ActorSystem _system;
        private readonly Serialization _serialization;
        private readonly IChannelProvider _channelProvider;
        private readonly TimeSpan? _withinTimeSpan;
        private string? _address;

        private CancellationTokenSource? _cancelFutureRetries;

        public EndpointSupervisor(EndpointManager endpointManager, RemoteConfig remoteConfig, ActorSystem system, Serialization serialization, IChannelProvider channelProvider)
        {
            _endpointManager = endpointManager;
            _remoteConfig = remoteConfig;
            _system = system;
            _serialization = serialization;
            _channelProvider = channelProvider;
            _maxNrOfRetries = remoteConfig.EndpointWriterOptions.MaxRetries;
            _withinTimeSpan = remoteConfig.EndpointWriterOptions.RetryTimeSpan;
            _backoff = TimeConvert.ToNanoseconds(remoteConfig.EndpointWriterOptions.RetryBackOffms);
        }

        public Task ReceiveAsync(IContext context)
        {
            if (context.Message is string address)
            {
                _address = address;
                var watcher = SpawnWatcher(address, context, _system, _endpointManager);
                var writer = SpawnWriter(address, context, _system, _serialization, _remoteConfig, _channelProvider);
                _cancelFutureRetries = new CancellationTokenSource();
                context.Respond(new Endpoint(writer, watcher));
            }

            return Actor.Done;
        }

        public void HandleFailure(
            ISupervisor supervisor, PID child, RestartStatistics rs, Exception reason,
            object? message
        )
        {
            if (ShouldStop(rs))
            {
                Logger.LogError(reason,
                    "Stopping connection to address {Address} after retries expired because of {Reason}",
                    _address, reason.GetType().Name
                );


                _cancelFutureRetries?.Cancel();
                supervisor.StopChildren(child);
                _system.ProcessRegistry.Remove(child); //TODO: work out why this hangs around in the process registry

                var terminated = new EndpointTerminatedEvent {Address = _address!};
                _system.EventStream.Publish(terminated);
            }
            else
            {
                var backoff = rs.FailureCount * _backoff;
                var noise = _random.Next(500);
                var duration = TimeSpan.FromMilliseconds(TimeConvert.ToMilliseconds(backoff + noise));

                _ = Task.Run(async () =>
                    {
                        await Task.Delay(duration);
                        Logger.LogWarning(reason,
                            "Restarting {Actor} after {Duration} because of {Reason}",
                            child.ToShortString(), duration, reason.GetType().Name
                        );
                        supervisor.RestartChildren(reason, child);
                    }
                    , _cancelFutureRetries!.Token
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

        private static PID SpawnWatcher(string address, ISpawnerContext context, ActorSystem system, EndpointManager endpointManager)
        {
            var watcherProps = Props.FromProducer(() => new EndpointWatcher(endpointManager, system, address));
            var watcher = context.Spawn(watcherProps);
            return watcher;
        }

        private static PID SpawnWriter(string address, ISpawnerContext context, ActorSystem system, Serialization serialization, RemoteConfig remoteConfig, IChannelProvider channelProvider)
        {
            var writerProps =
                Props.FromProducer(
                        () => new EndpointWriter(system, serialization,
                            remoteConfig,
                            address,
                            channelProvider
                        )
                    )
                    .WithMailbox(() =>
                        new EndpointWriterMailbox(system,
                            remoteConfig.EndpointWriterOptions.EndpointWriterBatchSize
                        )
                    );
            var writer = context.Spawn(writerProps);
            return writer;
        }
    }
}