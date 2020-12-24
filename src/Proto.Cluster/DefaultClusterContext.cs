// -----------------------------------------------------------------------
// <copyright file="DefaultClusterContext.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Cluster.IdentityLookup;
using Proto.Utils;

namespace Proto.Cluster
{
    public class DefaultClusterContext : IClusterContext
    {
        private readonly IIdentityLookup _identityLookup;
        private readonly ILogger _logger;
        private readonly PidCache _pidCache;
        private readonly ShouldThrottle _requestLogThrottle;

        public DefaultClusterContext(IIdentityLookup identityLookup, PidCache pidCache, ILogger logger)
        {
            _identityLookup = identityLookup;
            _pidCache = pidCache;
            _logger = logger;
            _requestLogThrottle = Throttle.Create(
                3,
                TimeSpan.FromSeconds(2),
                i => _logger.LogInformation("Throttled {LogCount} TryRequestAsync logs.", i)
            );
        }

        public async Task<T?> RequestAsync<T>(ClusterIdentity clusterIdentity, object message, ISenderContext context, CancellationToken ct)
        {
            
            _logger.LogDebug("Requesting {ClusterIdentity} Message {Message}", clusterIdentity.ToShortString(), message);
            var i = 0;
            while (!ct.IsCancellationRequested)
            {
                if (context.System.Shutdown.IsCancellationRequested)
                {
                    return default;
                }
                
                if (_pidCache.TryGet(clusterIdentity, out var cachedPid))
                {
                    _logger.LogDebug("Requesting {Identity}-{Kind} Message {Message} - Got PID {Pid} from PidCache",
                        clusterIdentity.Identity, clusterIdentity.Kind, message, cachedPid
                    );
                    var (status, res) = await TryRequestAsync<T>(clusterIdentity, message, cachedPid, "PidCache", context);
                    if (status == ResponseStatus.Ok) return res;
                }

                var delay = i * 20;
                i++;

                //try get a pid from id lookup
                try
                {
                    var pid = await _identityLookup.GetAsync(clusterIdentity, ct);
                    
                    if (context.System.Shutdown.IsCancellationRequested)
                    {
                        return default;
                    }
                    
                    if (pid is null)
                    {
                        _logger.LogDebug(
                            "Requesting {Identity}-{Kind} Message {Message} - Did not get PID from IdentityLookup",
                            clusterIdentity.Identity, clusterIdentity.Kind, message
                        );
                        await Task.Delay(delay, CancellationToken.None);
                        continue;
                    }

                    //got one, update cache
                    _pidCache.TryAdd(clusterIdentity, pid);

                    _logger.LogDebug(
                        "Requesting {Identity}-{Kind} Message {Message} - Got PID {PID} from IdentityLookup",
                        clusterIdentity.Identity, clusterIdentity.Kind, message, pid
                    );
                    
                    if (context.System.Shutdown.IsCancellationRequested)
                    {
                        return default;
                    }

                    var (status, res) = await TryRequestAsync<T>(clusterIdentity, message, pid, "IIdentityLookup", context);
                    switch (status)
                    {
                        case ResponseStatus.Ok:
                            return res;
                        case ResponseStatus.TimedOutOrException:
                            await Task.Delay(delay, CancellationToken.None);
                            break;
                        case ResponseStatus.DeadLetter:
                            await _identityLookup.RemovePidAsync(pid, ct);
                            break;
                    }
                }
                catch
                {
                    if (context.System.Shutdown.IsCancellationRequested)
                    {
                        return default;
                    }
                    
                    if (_requestLogThrottle().IsOpen())
                        _logger.LogWarning("Failed to get PID from IIdentityLookup");
                    await Task.Delay(delay, CancellationToken.None);
                }
            }
            //TODO: we should log here instead;

            return default!;
        }
        
        //TODO should this really log at all? these are transient issues. we could probably only fail when the method above gives up and returns 
        private async Task<(ResponseStatus ok, T res)> TryRequestAsync<T>(ClusterIdentity clusterIdentity,
            object message, PID cachedPid, string source, ISenderContext context)
        {
            try
            {
                var res = await context.RequestAsync<T>(cachedPid, message, TimeSpan.FromSeconds(5));

                if (res is not null) return (ResponseStatus.Ok, res);
            }
            catch (DeadLetterException)
            {
                if (!context.System.Shutdown.IsCancellationRequested && _requestLogThrottle().IsOpen())
                    _logger.LogInformation("TryRequestAsync failed, dead PID from {Source}", source);
                _pidCache.RemoveByVal(clusterIdentity, cachedPid);
                return (ResponseStatus.DeadLetter, default)!;
            }
            catch (TimeoutException)
            {
                if (!context.System.Shutdown.IsCancellationRequested && _requestLogThrottle().IsOpen())
                    _logger.LogWarning("TryRequestAsync timed out, PID from {Source}", source);
            }
            catch (Exception x)
            {
                if (!context.System.Shutdown.IsCancellationRequested && _requestLogThrottle().IsOpen())
                    _logger.LogWarning(x, "TryRequestAsync failed with exception, PID from {Source}", source);
            }

            _pidCache.RemoveByVal(clusterIdentity, cachedPid);

            return (ResponseStatus.TimedOutOrException, default)!;
        }

        private enum ResponseStatus
        {
            Ok,
            TimedOutOrException,
            DeadLetter
        }
    }
}