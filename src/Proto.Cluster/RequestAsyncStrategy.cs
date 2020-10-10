using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Cluster.IdentityLookup;

namespace Proto.Cluster
{
    public interface IClusterContext
    {
        Task<T> RequestAsync<T>(string identity, string kind, object message, CancellationToken ct);
    }

    public class DefaultClusterContext : IClusterContext
    {
        private readonly IIdentityLookup _identityLookup;
        private readonly PidCache _pidCache;
        private readonly ISenderContext _context;
        private readonly ILogger _logger;

        public DefaultClusterContext(IIdentityLookup identityLookup, PidCache pidCache, ISenderContext context, ILogger logger)
        {
            _identityLookup = identityLookup;
            _pidCache = pidCache;
            _context = context;
            _logger = logger;
        }
        
        void TryClearPidCache(string kind,string identity)
        {
            _logger.LogDebug(
                _pidCache.TryRemove(kind, identity)
                    ? "Removed {Kind}-{Identity} from PidCache"
                    : "Failed to remove {Kind}-{Identity} from PidCache", kind, identity
            );
        }

        public async Task<T> RequestAsync<T>(string identity, string kind, object message, CancellationToken ct)
        {
            _logger.LogDebug("Requesting {Identity}-{Kind} Message {Message}", identity, kind, message);
            var i = 0;
            while (!ct.IsCancellationRequested)
            {
                if (_pidCache.TryGet(kind, identity, out var cachedPid))
                {
                    _logger.LogDebug("Requesting {Identity}-{Kind} Message {Message} - Got PID {Pid} from PidCache",
                        identity,
                        kind, message, cachedPid
                    );
                    var (ok, res) = await TryRequestAsync<T>(identity, kind, message, cachedPid, "PidCache");
                    if (ok) return res;
                }
                
                var delay = i * 20;
                i++;

                //try get a pid from id lookup
                try
                {
                    var pid = await _identityLookup.GetAsync(identity, kind, ct);
                    if (pid == null)
                    {
                        _logger.LogDebug(
                            "Requesting {Identity}-{Kind} Message {Message} - Did not get PID from IdentityLookup",
                            identity, kind, message
                        );
                        await Task.Delay(delay, CancellationToken.None);
                        continue;
                    }

                    //got one, update cache
                    _pidCache.TryAdd(kind, identity, pid);

                    _logger.LogDebug(
                        "Requesting {Identity}-{Kind} Message {Message} - Got PID {PID} from IdentityLookup",
                        identity, kind, message, pid
                    );

                    var (ok, res) = await TryRequestAsync<T>(identity, kind, message, pid, "IIdentityLookup");
                    if (ok) return res;

                    await Task.Delay(delay, CancellationToken.None);
                }
                catch
                {
                    _logger.LogWarning("Failed to get PID from IIdentityLookup");
                    //failed to get pid from IdentityLookup
                    await Task.Delay(delay, CancellationToken.None);
                }
            }

            return default!;
        }

        private async Task<(bool ok,T res)> TryRequestAsync<T>(string identity, string kind, object message, PID cachedPid,string source)
        {
            try
            {
                var res = await _context.RequestAsync<T>(cachedPid, message, TimeSpan.FromSeconds(5));
                if (res != null)
                {
                    return (true,res);
                }
            }
            catch (TimeoutException)
            {
                _logger.LogWarning($"TryRequestAsync timed out, PID from {source}");
                
            }
            catch (Exception x)
            {
                _logger.LogWarning(x, $"TryRequestAsync failed with exception, PID from {source}");
                
            }
            TryClearPidCache(kind, identity);

            return (false,default)!;
        }
    }
}