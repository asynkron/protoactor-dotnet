// -----------------------------------------------------------------------
//   <copyright file="EndpointManager.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
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

        private readonly int _maxNrOfRetries;
        private readonly Random _random = new Random();
        private readonly ActorSystem _system;
        private readonly Remote _remote;
        private readonly TimeSpan? _withinTimeSpan;
        private CancellationTokenSource _cancelFutureRetries;

        private int _backoff;
        private string _address;

        public EndpointSupervisor(Remote remote, ActorSystem system)
        {
            _system = system;
            _remote = remote;
            _maxNrOfRetries = remote.RemoteConfig.EndpointWriterOptions.MaxRetries;
            _withinTimeSpan = remote.RemoteConfig.EndpointWriterOptions.RetryTimeSpan;
            _backoff = remote.RemoteConfig.EndpointWriterOptions.RetryBackOffms;
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
                _system.ProcessRegistry.Remove(child); //TODO: work out why this hangs around in the process registry

                var terminated = new EndpointTerminatedEvent { Address = _address };
                _system.EventStream.Publish(terminated);
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

        private static PID SpawnWatcher(string address, ISpawnerContext context, ActorSystem system, Remote remote)
        {
            var watcherProps = Props.FromProducer(() => new EndpointWatcher(remote, system, address));
            var watcher = context.Spawn(watcherProps);
            return watcher;
        }

        private static PID SpawnWriter(string address, ISpawnerContext context, ActorSystem system, Remote remote)
        {
            var writerProps =
                Props.FromProducer(
                        () => new EndpointWriter(system, remote.Serialization,
                            address, remote.RemoteConfig.ChannelOptions, remote.RemoteConfig.CallOptions, remote.RemoteConfig.ChannelCredentials
                        )
                    )
                    .WithMailbox(() => new EndpointWriterMailbox(system, remote.RemoteConfig.EndpointWriterOptions.EndpointWriterBatchSize));
            var writer = context.Spawn(writerProps);
            return writer;
        }
    }
}