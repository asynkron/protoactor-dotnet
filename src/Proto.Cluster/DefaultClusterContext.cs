// -----------------------------------------------------------------------
// <copyright file="DefaultClusterContext.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
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

namespace Proto.Cluster;

public class DefaultClusterContext : IClusterContext
{
    private readonly IIdentityLookup _identityLookup;

    private readonly PidCache _pidCache;
    private readonly ShouldThrottle _requestLogThrottle;
    private readonly TaskClock _clock;
    private readonly ActorSystem _system;

    private static readonly ILogger Logger = Log.CreateLogger<DefaultClusterContext>();

    public DefaultClusterContext(
        ActorSystem system,
        IIdentityLookup identityLookup,
        PidCache pidCache,
        ClusterContextConfig config,
        CancellationToken killSwitch
    )
    {
        _identityLookup = identityLookup;
        _pidCache = pidCache;
        _system = system;

        _requestLogThrottle = Throttle.Create(
            config.MaxNumberOfEventsInRequestLogThrottlePeriod,
            config.RequestLogThrottlePeriod,
            i => Logger.LogInformation("Throttled {LogCount} TryRequestAsync logs", i)
        );
        
        _clock = new TaskClock(config.ActorRequestTimeout, config.ActorRequestRetryInterval, killSwitch);
        _clock.Start();
    }

    public async Task<T?> RequestAsync<T>(ClusterIdentity clusterIdentity, object message, ISenderContext context, CancellationToken ct)
    {
        var start = Stopwatch.StartNew();
        if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("Requesting {ClusterIdentity} Message {Message}", clusterIdentity, message);
        var i = 0;

        var future = context.GetFuture();
        PID? lastPid = null;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (context.System.Shutdown.IsCancellationRequested) return default;

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
                    if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("Requesting {ClusterIdentity} - Did not get PID from IdentityLookup", clusterIdentity);
                    await Task.Delay(++i * 20, CancellationToken.None);
                    continue;
                }

                // Ensures that a future is not re-used against another actor.
                if (lastPid is not null && !pid.Equals(lastPid)) RefreshFuture();

                // Logger.LogDebug("Requesting {ClusterIdentity} - Got PID {Pid} from {Source}", clusterIdentity, pid, source);
                var (status, res) = await TryRequestAsync<T>(clusterIdentity, message, pid, source, context, future);

                switch (status)
                {
                    case ResponseStatus.Ok:
                        return res;

                    case ResponseStatus.Exception:
                        RefreshFuture();
                        await RemoveFromSource(clusterIdentity, PidSource.Cache, pid);
                        await Task.Delay(++i * 20, CancellationToken.None);
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

                if (_system.Metrics.Enabled)
                {
                    ClusterMetrics.ClusterRequestRetryCount.Add(
                        1, new("id", _system.Id), new("address", _system.Address),
                        new("clusterkind", clusterIdentity.Kind), new("messagetype", message.GetType().Name)
                    );
                }
            }

            if (!context.System.Shutdown.IsCancellationRequested && _requestLogThrottle().IsOpen())
            {
                var t = start.Elapsed;
                Logger.LogWarning("RequestAsync retried but failed for {ClusterIdentity}, elapsed {Time}", clusterIdentity, t);
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

    private async Task<PID?> GetPidFromLookup(ClusterIdentity clusterIdentity, ISenderContext context, CancellationToken ct)
    {
        try
        {
            if (context.System.Metrics.Enabled)
            {
                var pid = await ClusterMetrics.ClusterResolvePidDuration
                    .Observe(
                        async () => await _identityLookup.GetAsync(clusterIdentity, ct),
                        new("id", _system.Id), new("address", _system.Address), new("clusterkind", clusterIdentity.Kind)
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
        catch (Exception e) when(e is not IdentityIsBlocked)
        {
            e.CheckFailFast();
            if (context.System.Shutdown.IsCancellationRequested) return default;

            if (_requestLogThrottle().IsOpen())
                Logger.LogWarning(e, "Failed to get PID from IIdentityLookup for {ClusterIdentity}", clusterIdentity);
            return null;
        }
    }

    private async ValueTask<(ResponseStatus Status, T?)> TryRequestAsync<T>(
        ClusterIdentity clusterIdentity,
        object message,
        PID pid,
        PidSource source,
        ISenderContext context,
        IFuture future
    )
    {
        var t = Stopwatch.StartNew();

        try
        {
            context.Request(pid, message, future.Pid);
            var task = future.Task;

            await Task.WhenAny(task, _clock.CurrentBucket);

            if (task.IsCompleted)
            {
                var res = task.Result;

                return ToResult<T>(source, context, res);
            }

            if (!context.System.Shutdown.IsCancellationRequested)
                if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("TryRequestAsync timed out, PID from {Source}", source);

            _pidCache.RemoveByVal(clusterIdentity, pid);
            
            return (ResponseStatus.TimedOut, default);
        }
        catch (TimeoutException)
        {
            return (ResponseStatus.TimedOut, default);
        }
        catch (Exception x)
        {
            x.CheckFailFast();
            if (!context.System.Shutdown.IsCancellationRequested && _requestLogThrottle().IsOpen())
                if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug(x, "TryRequestAsync failed with exception, PID from {Source}", source);
            _pidCache.RemoveByVal(clusterIdentity, pid);
            return (ResponseStatus.Exception, default);
        }
        finally
        {
            if (context.System.Metrics.Enabled)
            {
                var elapsed = t.Elapsed;
                ClusterMetrics.ClusterRequestDuration
                    .Record(elapsed.TotalSeconds,
                        new("id", _system.Id), new("address", _system.Address),
                        new("clusterkind", clusterIdentity.Kind), new("messagetype", message.GetType().Name),
                        new("pidsource", source == PidSource.Cache ? "PidCache" : "IIdentityLookup")
                    );
            }
        }
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

    private static (ResponseStatus Status, T?) ToResult<T>(PidSource source, ISenderContext context, object result)
    {
        var message = MessageEnvelope.UnwrapMessage(result);

        switch (message)
        {
            case DeadLetterResponse:
                if (!context.System.Shutdown.IsCancellationRequested)
                    if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("TryRequestAsync failed, dead PID from {Source}", source);

                return (ResponseStatus.DeadLetter, default);
            case null: return (ResponseStatus.Ok, default);
            case T t:  return (ResponseStatus.Ok, t);
            default:
                if (typeof(T) == typeof(MessageEnvelope))
                {
                    return (ResponseStatus.Ok, (T) (object) MessageEnvelope.Wrap(result));
                }
                Logger.LogWarning("Unexpected message. Was type {Type} but expected {ExpectedType}", message.GetType(), typeof(T));
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