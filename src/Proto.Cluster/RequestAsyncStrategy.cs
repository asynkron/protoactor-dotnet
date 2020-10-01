using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Cluster.IdentityLookup;

namespace Proto.Cluster
{
    public interface IRequestAsyncStrategy
    {
        Task<T> RequestAsync<T>(string identity, string kind, object message, CancellationToken ct);
    }

    public class RequestAsyncStrategy : IRequestAsyncStrategy
    {
        private readonly IIdentityLookup _identityLookup;
        private readonly PidCache _pidCache;
        private readonly ISenderContext _context;
        private readonly ILogger _logger;

        public RequestAsyncStrategy(IIdentityLookup identityLookup, PidCache pidCache, ISenderContext context, ILogger logger)
        {
            _identityLookup = identityLookup;
            _pidCache = pidCache;
            _context = context;
            _logger = logger;
        }

        public async Task<T> RequestAsync<T>(string identity, string kind, object message, CancellationToken ct)
        { 
            _logger.LogDebug("Requesting {Identity}-{Kind} Message {Message}", identity, kind, message);
            var i = 0;
            while (!ct.IsCancellationRequested)
            {
                var hadPid = _pidCache.TryGet(kind, identity, out var cachedPid);
                try
                {
                    if (hadPid)
                    {
                        _logger.LogDebug("Requesting {Identity}-{Kind} Message {Message} - Got PID from cache {Pid}",
                            identity,
                            kind, message, cachedPid
                        );
                        var res = await _context.RequestAsync<T>(cachedPid, message, ct);
                        if (res != null)
                        {
                            return res;
                        }

                        _pidCache.TryRemove(kind, identity);
                    }
                }
                catch
                {
                    _pidCache.TryRemove(kind, identity);
                }

                var delay = i * 20;
                i++;
                var pid = await _identityLookup.GetAsync(identity, kind, ct);

                if (pid == null)
                {
                    _logger.LogDebug(
                        "Requesting {Identity}-{Kind} Message {Message} - Did not get any PID from IdentityLookup",
                        identity, kind, message
                    );
                    await Task.Delay(delay, CancellationToken.None);
                    continue;
                }

                _logger.LogDebug("Requesting {Identity}-{Kind} Message {Message} - Got PID {PID} from IdentityLookup",
                    identity, kind, message, pid
                );
                //update cache
                _pidCache.TryAdd(kind, identity, pid); //TODO: Responsibility of identity lookup?

                var res2 = await _context.RequestAsync<T>(pid, message, ct);
                if (res2 != null) return res2;
                
                _pidCache.TryRemove(kind, identity);
                await Task.Delay(delay, CancellationToken.None);
            }

            return default!;
        }
    }
}