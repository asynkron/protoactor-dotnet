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
        private readonly TimeSpan _backoff;

        private readonly int _maxNrOfRetries;
        private readonly Random _random = new Random();
        private readonly Remote _remote;
        private readonly ActorSystem _system;
        private readonly TimeSpan? _withinTimeSpan;
        private string? _address;

        private CancellationTokenSource? _cancelFutureRetries;

        public EndpointSupervisor(Remote remote, ActorSystem system)
        {
            if (remote.Config is null)
            {
                throw new ArgumentException("RemoteConfig may not be null", nameof(remote));
            }

            _system = system;
            _remote = remote;
            _maxNrOfRetries = remote.Config.EndpointWriterOptions.MaxRetries;
            _withinTimeSpan = remote.Config.EndpointWriterOptions.RetryTimeSpan;
            _backoff = remote.Config.EndpointWriterOptions.RetryBackOff;
        }

        public Task ReceiveAsync(IContext context)
        {
            if (context.Message is string address)
            {
                _address = address;
                var watcher = SpawnWatcher(address, context, _system, _remote);
                var writer = SpawnWriter(address, context, _system, _remote);
                _cancelFutureRetries = new CancellationTokenSource();
                context.Respond(new Endpoint(writer, watcher));
            }

            return Task.CompletedTask;
        }

        public void HandleFailure(
            ISupervisor supervisor, PID child, RestartStatistics rs, Exception reason,
            object? message
        )
        {
            if (ShouldStop(rs))
            {
                Logger.LogError(
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
                var backoff = rs.FailureCount * (int)_backoff.TotalMilliseconds;
                var noise = _random.Next(500);
                var duration = TimeSpan.FromMilliseconds(backoff + noise);

                _ = Task.Run(async () =>
                    {
                        await Task.Delay(duration);
                        Logger.LogWarning(
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

        private static PID SpawnWatcher(string address, ISpawnerContext context, ActorSystem system, Remote remote)
        {
            var watcherProps = Props.FromProducer(() => new EndpointWatcher(remote, system, address));
            var watcher = context.Spawn(watcherProps);
            return watcher;
        }

        private static PID SpawnWriter(string address, ISpawnerContext context, ActorSystem system, Remote remote)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentNullException(nameof(address));
            }
            
            if (remote.Config is null)
            {
                throw new ArgumentNullException(nameof(remote));
            }
            
            var writerProps =
                Props.FromProducer(
                        () => new EndpointWriter(system, remote.Config.Serialization,
                            address, remote.Config.ChannelOptions, 
                            remote.Config.CallOptions,
                            remote.Config.ChannelCredentials
                        )
                    )
                    .WithMailbox(() =>
                        new EndpointWriterMailbox(system,
                            remote.Config.EndpointWriterOptions.EndpointWriterBatchSize
                        )
                    );
            var writer = context.Spawn(writerProps);
            return writer;
        }
    }
}