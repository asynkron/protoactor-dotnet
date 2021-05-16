// -----------------------------------------------------------------------
// <copyright file="ExperimentalClusterContext.cs" company="Asynkron AB">
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
using Proto.Utils;

namespace Proto.Cluster
{
    public class ExperimentalClusterContext : IClusterContext
    {
        private readonly IIdentityLookup _identityLookup;

        private readonly PidCache _pidCache;
        private readonly ShouldThrottle _requestLogThrottle;
        private readonly TaskClock _clock;
        private static readonly ILogger Logger = Log.CreateLogger<ExperimentalClusterContext>();

        public ExperimentalClusterContext(Cluster cluster)
        {
            _identityLookup = cluster.IdentityLookup;
            _pidCache = cluster.PidCache;
            var config = cluster.Config;

            _requestLogThrottle = Throttle.Create(
                config.MaxNumberOfEventsInRequestLogThrottlePeriod,
                config.RequestLogThrottlePeriod,
                i => Logger.LogInformation("Throttled {LogCount} TryRequestAsync logs", i)
            );
            _clock = new TaskClock(config.ActorRequestTimeout, TimeSpan.FromSeconds(1), cluster.System.Shutdown);
            _clock.Start();
        }

        public async Task<T?> RequestAsync<T>(ClusterIdentity clusterIdentity, object message, ISenderContext context, CancellationToken ct)
        {
            var start = Stopwatch.StartNew();
            var i = 0;

            var future = context.GetFuture();
            PID? lastPid = null;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    if (context.System.Shutdown.IsCancellationRequested) return default;

                    i++;

                    var source = PidSource.Cache;
                    var pid = GetCachedPid(clusterIdentity);

                    if (pid is null)
                    {
                        source = PidSource.Lookup;
                        pid = await GetPidFromLookup(clusterIdentity, context, ct);
                    }

                    if (context.System.Shutdown.IsCancellationRequested) return default;

                    if (pid is null)
                    {
                        Logger.LogDebug("Requesting {ClusterIdentity} - Did not get PID from IdentityLookup", clusterIdentity);
                        await Task.Delay(i * 20, CancellationToken.None);
                        continue;
                    }

                    // Ensures that a future is not re-used against another actor.
                    if (lastPid is not null && !pid.Equals(lastPid)) RefreshFuture();

                    var t = Stopwatch.StartNew();

                    try
                    {
                        context.Request(pid, message, future.Pid);
                        var task = future.Task;

                        await Task.WhenAny(task, _clock.CurrentBucket);

                        if (task.IsCompleted)
                        {
                            var (status, result) = ToResult<T>(source, context, task.Result);

                            switch (status)
                            {
                                case ResponseStatus.Ok: return result;
                                case ResponseStatus.InvalidResponse:
                                    RefreshFuture();
                                    await RemoveFromSource(clusterIdentity, source, pid);
                                    break;
                                case ResponseStatus.DeadLetter:
                                    RefreshFuture();
                                    await RemoveFromSource(clusterIdentity, PidSource.Lookup, pid);
                                    break;
                            }
                        }
                        else
                        {
                            if (!context.System.Shutdown.IsCancellationRequested)
                                Logger.LogDebug("TryRequestAsync timed out, PID from {Source}", source);
                            _pidCache.RemoveByVal(clusterIdentity, pid);
                        }
                    }
                    catch (TimeoutException)
                    {
                        lastPid = pid;
                        await RemoveFromSource(clusterIdentity, PidSource.Cache, pid);
                        continue;
                    }
                    catch (Exception x)
                    {
                        if (!context.System.Shutdown.IsCancellationRequested && _requestLogThrottle().IsOpen())
                            Logger.LogDebug(x, "TryRequestAsync failed with exception, PID from {Source}", source);
                        _pidCache.RemoveByVal(clusterIdentity, pid);
                        RefreshFuture();
                        await RemoveFromSource(clusterIdentity, PidSource.Cache, pid);
                        await Task.Delay(i * 20, CancellationToken.None);
                        continue;
                    }
                    finally
                    {
                        if (!context.System.Metrics.IsNoop)
                        {
                            var elapsed = t.Elapsed;
                            context.System.Metrics.Get<ClusterMetrics>().ClusterRequestHistogram
                                .Observe(elapsed, new[]
                                    {
                                        context.System.Id, context.System.Address, clusterIdentity.Kind, message.GetType().Name,
                                        source == PidSource.Cache ? "PidCache" : "IIdentityLookup"
                                    }
                                );
                        }
                    }

                    if (!context.System.Metrics.IsNoop)
                    {
                        context.System.Metrics.Get<ClusterMetrics>().ClusterRequestRetryCount.Inc(new[]
                            {context.System.Id, context.System.Address, clusterIdentity.Kind, message.GetType().Name}
                        );
                    }
                }

                if (!context.System.Shutdown.IsCancellationRequested && _requestLogThrottle().IsOpen())
                {
                    Logger.LogWarning("RequestAsync retried but failed for {ClusterIdentity}, elapsed {Time}", clusterIdentity, start.Elapsed);
                }

                return default!;
            }
            finally
            {
                future.Dispose();
            }

            void RefreshFuture()
            {
                future.Dispose();
                future = context.GetFuture();
                lastPid = null;
            }
        }

        private async ValueTask RemoveFromSource(ClusterIdentity clusterIdentity, PidSource source, PID pid)
        {
            if (source == PidSource.Lookup) await _identityLookup.RemovePidAsync(clusterIdentity, pid, CancellationToken.None);

            _pidCache.RemoveByVal(clusterIdentity, pid);
        }


        private PID? GetCachedPid(ClusterIdentity clusterIdentity)
        {
            var pid = clusterIdentity.CachedPid;

            if (pid is null && _pidCache.TryGet(clusterIdentity, out pid))
            {
                clusterIdentity.CachedPid = pid;
            }

            return pid;
        }

        private async Task<PID?> GetPidFromLookup(ClusterIdentity clusterIdentity, ISenderContext context, CancellationToken ct)
        {
            try
            {
                if (!context.System.Metrics.IsNoop)
                {
                    var pid = await context.System.Metrics.Get<ClusterMetrics>().ClusterResolvePidHistogram
                        .Observe(async () => await _identityLookup.GetAsync(clusterIdentity, ct), context.System.Id, context.System.Address,
                            clusterIdentity.Kind
                        );

                    if (pid is not null) _pidCache.TryAdd(clusterIdentity, pid);
                    return pid;
                }
                else
                {
                    var pid = await _identityLookup.GetAsync(clusterIdentity, ct);
                    if (pid is not null) _pidCache.TryAdd(clusterIdentity, pid);
                    return pid;
                }
            }
            catch (Exception e)
            {
                if (context.System.Shutdown.IsCancellationRequested) return default;

                if (_requestLogThrottle().IsOpen())
                    Logger.LogWarning(e, "Failed to get PID from IIdentityLookup for {ClusterIdentity}", clusterIdentity);
                return null;
            }
        }

        private static (ResponseStatus Ok, T?) ToResult<T>(PidSource source, ISenderContext context, object result)
        {
            switch (result)
            {
                case DeadLetterResponse:
                    if (!context.System.Shutdown.IsCancellationRequested)
                        Logger.LogDebug("TryRequestAsync failed, dead PID from {Source}", source);

                    return (ResponseStatus.DeadLetter, default)!;
                case null: return (ResponseStatus.Ok, default);
                case T t:  return (ResponseStatus.Ok, t);
                default:
                    Logger.LogError("Unexpected message. Was type {Type} but expected {ExpectedType}", result.GetType(), typeof(T));
                    return (ResponseStatus.InvalidResponse, default);
            }
        }

        private enum ResponseStatus
        {
            Ok,
            InvalidResponse,
            DeadLetter
        }

        private enum PidSource
        {
            Cache,
            Lookup
        }
    }
}