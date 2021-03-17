// -----------------------------------------------------------------------
// <copyright file="DefaultClusterContext.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Cluster.Identity;
using Proto.Cluster.Metrics;
using Proto.Future;
using Proto.Utils;

namespace Proto.Cluster
{
    public class DefaultClusterContext : IClusterContext
    {
        private readonly IIdentityLookup _identityLookup;
        private readonly ILogger _logger;
        private readonly ClusterContextConfig _config;
        private readonly PidCache _pidCache;
        private readonly ShouldThrottle _requestLogThrottle;

        public DefaultClusterContext(IIdentityLookup identityLookup, PidCache pidCache, ILogger logger, ClusterContextConfig config)
        {
            _identityLookup = identityLookup;
            _pidCache = pidCache;
            _logger = logger;
            _config = config;
            _requestLogThrottle = Throttle.Create(
                _config.MaxNumberOfEventsInRequestLogThrottlePeriod,
                _config.RequestLogThrottlePeriod,
                i => _logger.LogInformation("Throttled {LogCount} TryRequestAsync logs", i)
            );
        }

        public async Task<T?> RequestAsync<T>(ClusterIdentity clusterIdentity, object message, ISenderContext context, CancellationToken ct)
        {
            _logger.LogDebug("Requesting {ClusterIdentity} Message {Message}", clusterIdentity, message);
            var i = 0;

            var future = new FutureProcess(context.System, ct);
            PID? lastPid = null;

            while (!ct.IsCancellationRequested)
            {
                if (context.System.Shutdown.IsCancellationRequested) return default;

                var delay = i * 20;
                i++;
                var (pid, source) = await GetPid(clusterIdentity, context, ct);
                if (context.System.Shutdown.IsCancellationRequested) return default;

                if (pid is null)
                {
                    _logger.LogDebug("Requesting {ClusterIdentity} - Did not get PID from IdentityLookup", clusterIdentity);
                    await Task.Delay(delay, CancellationToken.None);
                    continue;
                }

                // Ensures that a future is not re-used against another actor.
                if (lastPid is not null && !pid.Equals(lastPid)) RefreshFuture();

                _logger.LogDebug("Requesting {ClusterIdentity} - Got PID {Pid} from {Source}", clusterIdentity, pid, source);
                var (status, res) = await TryRequestAsync<T>(clusterIdentity, message, pid, source, context, future);

                switch (status)
                {
                    case ResponseStatus.Ok:
                        return res;

                    case ResponseStatus.Exception:
                        RefreshFuture();
                        await RemoveFromSource(clusterIdentity, PidSource.Cache, pid);
                        await Task.Delay(delay, CancellationToken.None);
                        break;
                    case ResponseStatus.DeadLetter:
                        RefreshFuture();
                        await RemoveFromSource(clusterIdentity, source, pid);
                        break;
                    case ResponseStatus.TimedOut:
                        lastPid = pid;
                        await RemoveFromSource(clusterIdentity, PidSource.Cache, pid);
                        break;
                }

                context.System.Metrics.Get<ClusterMetrics>()?.ClusterRequestRetryCount.Inc(new[]
                    {context.System.Id, context.System.Address, clusterIdentity.Kind, message.GetType().Name}
                );
            }

            if (!context.System.Shutdown.IsCancellationRequested && _requestLogThrottle().IsOpen())
                _logger.LogWarning("RequestAsync retried but failed for {ClusterIdentity}", clusterIdentity);

            return default!;

            void RefreshFuture()
            {
                future = new FutureProcess(context.System, ct);
                lastPid = null;
            }
        }

        private async Task RemoveFromSource(ClusterIdentity clusterIdentity, PidSource source, PID pid)
        {
            if (source == PidSource.Lookup)
            {
                await _identityLookup.RemovePidAsync(pid, CancellationToken.None);
            }

            _pidCache.RemoveByVal(clusterIdentity, pid);
        }

        private async ValueTask<(PID?, PidSource)> GetPid(ClusterIdentity clusterIdentity, ISenderContext context, CancellationToken ct)
        {
            try
            {
                if (_pidCache.TryGet(clusterIdentity, out var cachedPid))
                {
                    return (cachedPid, PidSource.Cache);
                }

                var pid = await _identityLookup.GetAsync(clusterIdentity, ct);
                if (pid is not null) _pidCache.TryAdd(clusterIdentity, pid);
                return (pid, PidSource.Lookup);
            }
            catch (Exception e)
            {
                if (context.System.Shutdown.IsCancellationRequested) return default;

                if (_requestLogThrottle().IsOpen())
                    _logger.LogWarning(e, "Failed to get PID from IIdentityLookup for {ClusterIdentity}", clusterIdentity);
                return (null, PidSource.Lookup);
            }
        }

        private async Task<(ResponseStatus Ok, T?)> TryRequestAsync<T>(
            ClusterIdentity clusterIdentity,
            object message,
            PID pid,
            PidSource source,
            ISenderContext context,
            FutureProcess future
        )
        {
            try
            {
                if (future.Task.IsCompleted)
                {
                    return ToResult<T>(source, context, future.Task.Result);
                }

                var sw = Stopwatch.StartNew();

                context.Send(pid, new MessageEnvelope(message, future.Pid));
                await Task.WhenAny(future.Task, Task.Delay(_config.ActorRequestTimeout));
                context.System.Metrics.Get<ClusterMetrics>()?.ClusterRequestHistogram
                    .Observe(sw,
                        new[]
                        {
                            context.System.Id, context.System.Address, clusterIdentity.Kind, message.GetType().Name,
                            source == PidSource.Cache ? "PidCache" : "IIdentityLookup"
                        }
                    );

                if (future.Task.IsCompleted)
                {
                    var res = future.Task.Result;

                    return ToResult<T>(source, context, res);
                }

                if (!context.System.Shutdown.IsCancellationRequested)
                    _logger.LogDebug("TryRequestAsync timed out, PID from {Source}", source);
                _pidCache.RemoveByVal(clusterIdentity, pid);

                return (ResponseStatus.TimedOut, default)!;
            }
            catch (TimeoutException)
            {
                return (ResponseStatus.TimedOut, default)!;
            }
            catch (Exception x)
            {
                if (!context.System.Shutdown.IsCancellationRequested && _requestLogThrottle().IsOpen())
                    _logger.LogDebug(x, "TryRequestAsync failed with exception, PID from {Source}", source);
                _pidCache.RemoveByVal(clusterIdentity, pid);
                return (ResponseStatus.Exception, default)!;
            }
        }

        private (ResponseStatus Ok, T?) ToResult<T>(PidSource source, ISenderContext context, object result)
        {
            switch (result)
            {
                case DeadLetterResponse:
                    if (!context.System.Shutdown.IsCancellationRequested)
                        _logger.LogDebug("TryRequestAsync failed, dead PID from {Source}", source);

                    return (ResponseStatus.DeadLetter, default)!;
                case null: return (ResponseStatus.Ok, default);
                case T t:  return (ResponseStatus.Ok, t);
                default:
                    _logger.LogWarning("Unexpected message. Was type {Type} but expected {ExpectedType}", result.GetType(), typeof(T));
                    return (ResponseStatus.Exception, default);
            }
        }

        private async Task<(ResponseStatus ok, T res)> HandleDeadLetter<T>(
            PidSource source,
            ISenderContext context
        )
        {
            if (!context.System.Shutdown.IsCancellationRequested)
                _logger.LogDebug("TryRequestAsync failed, dead PID from {Source}", source);

            return (ResponseStatus.DeadLetter, default)!;
        }

        private enum ResponseStatus
        {
            Ok,
            TimedOut,
            Exception,
            DeadLetter
        }

        private enum PidSource
        {
            Cache,
            Lookup
        }
    }
}