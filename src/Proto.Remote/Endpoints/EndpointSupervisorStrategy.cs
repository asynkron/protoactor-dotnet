// -----------------------------------------------------------------------
//   <copyright file="EndpointSupervisorStrategy.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Proto.Remote
{
    public class EndpointSupervisorStrategy : ISupervisorStrategy
    {
        private static readonly ILogger Logger = Log.CreateLogger<EndpointSupervisorStrategy>();
        private readonly string _address;
        private readonly TimeSpan _backoff;
        private readonly CancellationTokenSource _cancelFutureRetries = new();

        private readonly int _maxNrOfRetries;
        private readonly Random _random = new();
        private readonly ActorSystem _system;
        private readonly TimeSpan? _withinTimeSpan;

        public EndpointSupervisorStrategy(string address, RemoteConfigBase remoteConfig, ActorSystem system)
        {
            _address = address;
            _system = system;
            _maxNrOfRetries = remoteConfig.EndpointWriterOptions.MaxRetries;
            _withinTimeSpan = remoteConfig.EndpointWriterOptions.RetryTimeSpan;
            _backoff = remoteConfig.EndpointWriterOptions.RetryBackOff;
        }

        public void HandleFailure(
            ISupervisor supervisor,
            PID child,
            RestartStatistics rs,
            Exception reason,
            object? message
        )
        {
            if (ShouldStop(rs))
            {
                Logger.LogError(
                    "[EndpointSupervisor] Stopping connection to address {Address} after retries expired because of {Reason}",
                    _address, reason.GetType().Name
                );
                _cancelFutureRetries.Cancel();
                var terminated = new EndpointTerminatedEvent {Address = _address!};
                _system.EventStream.Publish(terminated);
            }
            else
            {
                var backoff = rs.FailureCount * (int) _backoff.TotalMilliseconds;
                var noise = _random.Next(500);
                var duration = TimeSpan.FromMilliseconds(backoff + noise);

                _ = SafeTask.Run(async () => {
                        await Task.Delay(duration);

                        if (reason is RpcException rpc && rpc.StatusCode == StatusCode.Unavailable)
                        {
                            Logger.LogWarning(
                                "[EndpointSupervisor] Restarting {Actor} after {Duration} because endpoint is unavailable",
                                child, duration
                            );
                        }
                        else
                        {
                            Logger.LogWarning(reason,
                                "[EndpointSupervisor] Restarting {Actor} after {Duration} because of {Reason}",
                                child, duration, reason.GetType().Name
                            );
                        }
                   
                        supervisor.RestartChildren(reason, child);
                    }
                    , _cancelFutureRetries.Token
                );
            }
        }

        private bool ShouldStop(RestartStatistics rs)
        {
            if (_maxNrOfRetries == 0) return true;

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