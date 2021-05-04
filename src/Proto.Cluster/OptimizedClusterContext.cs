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
    public class OptimizedClusterContext : IClusterContext
    {
        private readonly IIdentityLookup _identityLookup;

        private readonly PidCache _pidCache;
        private readonly ShouldThrottle _requestLogThrottle;
        // private readonly TaskClock _clock;
        private static readonly ILogger Logger = Log.CreateLogger<OptimizedClusterContext>();

        public OptimizedClusterContext(Cluster cluster)
        {
            _identityLookup = cluster.IdentityLookup;
            _pidCache = cluster.PidCache;
            var config = cluster.Config;

            _requestLogThrottle = Throttle.Create(
                config.MaxNumberOfEventsInRequestLogThrottlePeriod,
                config.RequestLogThrottlePeriod,
                i => Logger.LogInformation("Throttled {LogCount} TryRequestAsync logs", i)
            );
            // _clock = new TaskClock(config.ActorRequestTimeout, TimeSpan.FromSeconds(1), cluster.System.Shutdown);
            // _clock.Start();
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

                    var pid = clusterIdentity.CachedPid;
                    var source = PidSource.Cache;

                    if (pid is null)
                    {
                        (pid, source) = await GetPid(clusterIdentity, context, ct);
                    }

                    if (context.System.Shutdown.IsCancellationRequested) return default;

                    if (pid is null)
                    {
                        Logger.LogDebug("Requesting {ClusterIdentity} - Did not get PID from IdentityLookup", clusterIdentity);
                        await Task.Delay(++i * 20, CancellationToken.None);
                        continue;
                    }

                    // Ensures that a future is not re-used against another actor.
                    if (lastPid is not null && !pid.Equals(lastPid)) RefreshFuture();


                    ResponseStatus status;
                    
                    var t = DateTimeOffset.UtcNow;

                    try
                    {
                        context.Request(pid, message, future.Pid);
                        var task = future.Task;
                        var res = await task;
                        T? result;
                        (status, result) = ToResult<T>(source, context, res);
                        if (status == ResponseStatus.Ok) return result;

                        if (status == ResponseStatus.DeadLetter)
                        {
                            RefreshFuture();
                            await RemoveFromSource(clusterIdentity, source, pid);
                            continue;
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
                        await Task.Delay(++i * 20, CancellationToken.None);
                        continue;
                    }
                    finally
                    {
                        if (!context.System.Metrics.IsNoop)
                        {
                            var elapsed = DateTimeOffset.UtcNow - t;
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

        private async ValueTask<(PID?, PidSource)> GetPid(ClusterIdentity clusterIdentity, ISenderContext context, CancellationToken ct)
        {
            try
            {
                if (_pidCache.TryGet(clusterIdentity, out var cachedPid)) return (cachedPid, PidSource.Cache);

                if (!context.System.Metrics.IsNoop)
                {
                    var pid = await context.System.Metrics.Get<ClusterMetrics>().ClusterResolvePidHistogram
                        .Observe(async () => await _identityLookup.GetAsync(clusterIdentity, ct), context.System.Id, context.System.Address,
                            clusterIdentity.Kind
                        );

                    if (pid is not null) _pidCache.TryAdd(clusterIdentity, pid);
                    return (pid, PidSource.Lookup);
                }
                else
                {
                    var pid = await _identityLookup.GetAsync(clusterIdentity, ct);
                    if (pid is not null) _pidCache.TryAdd(clusterIdentity, pid);
                    return (pid, PidSource.Lookup);
                }
            }
            catch (Exception e)
            {
                if (context.System.Shutdown.IsCancellationRequested) return default;

                if (_requestLogThrottle().IsOpen())
                    Logger.LogWarning(e, "Failed to get PID from IIdentityLookup for {ClusterIdentity}", clusterIdentity);
                return (null, PidSource.Lookup);
            }
        }

        // private async ValueTask<(ResponseStatus Ok, T?)> TryRequestAsync<T>(
        //     ClusterIdentity clusterIdentity,
        //     object message,
        //     PID pid,
        //     PidSource source,
        //     ISenderContext context,
        //     IFuture future
        // )
        // {
        //     var t = DateTimeOffset.UtcNow;
        //
        //     try
        //     {
        //         context.Request(pid, message, future.Pid);
        //         var task = future.Task;
        //         var res = await task;
        //         return ToResult<T>(source, context, res);
        //     }
        //     catch (TimeoutException)
        //     {
        //         return (ResponseStatus.TimedOut, default)!;
        //     }
        //     catch (Exception x)
        //     {
        //         if (!context.System.Shutdown.IsCancellationRequested && _requestLogThrottle().IsOpen())
        //             Logger.LogDebug(x, "TryRequestAsync failed with exception, PID from {Source}", source);
        //         _pidCache.RemoveByVal(clusterIdentity, pid);
        //         return (ResponseStatus.Exception, default)!;
        //     }
        //     finally
        //     {
        //         if (!context.System.Metrics.IsNoop)
        //         {
        //             var elapsed = DateTimeOffset.UtcNow - t;
        //             context.System.Metrics.Get<ClusterMetrics>().ClusterRequestHistogram
        //                 .Observe(elapsed, new[]
        //                     {
        //                         context.System.Id, context.System.Address, clusterIdentity.Kind, message.GetType().Name,
        //                         source == PidSource.Cache ? "PidCache" : "IIdentityLookup"
        //                     }
        //                 );
        //         }
        //     }
        // }

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
                    Logger.LogWarning("Unexpected message. Was type {Type} but expected {ExpectedType}", result.GetType(), typeof(T));
                    return (ResponseStatus.Exception, default);
            }
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