// -----------------------------------------------------------------------
//   <copyright file="EndpointSupervisorStrategy.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto.Remote
{
    public class EndpointSupervisorStrategy : ISupervisorStrategy
    {
        private static readonly ILogger Logger = Log.CreateLogger<EndpointSupervisorStrategy>();
        private readonly long _backoff;
        private readonly int _maxNrOfRetries;
        private readonly Random _random = new Random();
        private readonly ActorSystem _system;
        private readonly TimeSpan? _withinTimeSpan;
        private string? _address;
        private CancellationTokenSource? _cancelFutureRetries = new CancellationTokenSource();
        public EndpointSupervisorStrategy(string address, RemoteConfig remoteConfig, ActorSystem system)
        {
            _address = address;
            _system = system;
            _maxNrOfRetries = remoteConfig.EndpointWriterOptions.MaxRetries;
            _withinTimeSpan = remoteConfig.EndpointWriterOptions.RetryTimeSpan;
            _backoff = TimeConvert.ToNanoseconds(remoteConfig.EndpointWriterOptions.RetryBackOffms);
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
                var terminated = new EndpointTerminatedEvent { Address = _address! };
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
    }
}