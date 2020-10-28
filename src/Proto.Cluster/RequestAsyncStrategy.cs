namespace Proto.Cluster
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using IdentityLookup;
    using Microsoft.Extensions.Logging;
    using Proto.Utils;

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
        private readonly ShouldThrottle _requestLogThrottle = Throttle.Create(10, TimeSpan.FromSeconds(5));

        public DefaultClusterContext(IIdentityLookup identityLookup, PidCache pidCache, ISenderContext context,
            ILogger logger)
        {
            _identityLookup = identityLookup;
            _pidCache = pidCache;
            _context = context;
            _logger = logger;
        }

        void TryClearPidCache(string kind, string identity)
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
                    var (status, res) = await TryRequestAsync<T>(identity, kind, message, cachedPid, "PidCache");
                    if (status == ResponseStatus.Ok) return res;
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

                    var (status, res) = await TryRequestAsync<T>(identity, kind, message, pid, "IIdentityLookup");
                    switch (status)
                    {
                        case ResponseStatus.Ok:
                            return res;
                        case ResponseStatus.TimedOutOrException:
                            await Task.Delay(delay, CancellationToken.None);
                            break;
                        case ResponseStatus.DeadLetter:
                            //No need to delay since we know that the actor is removed.
                            break;
                    }
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

        private async Task<(ResponseStatus ok, T res)> TryRequestAsync<T>(string identity, string kind, object message,
            PID cachedPid, string source)
        {
            try
            {
                var res = await _context.RequestAsync<T>(cachedPid, message, TimeSpan.FromSeconds(5));

                if (res != null)
                {
                    return (ResponseStatus.Ok, res);
                }
            }
            catch (DeadLetterException)
            {
                if (_requestLogThrottle().IsOpen())
                {
                    _logger.LogInformation("TryRequestAsync failed, dead PID from {Source}", source);
                }
                _pidCache.RemoveByVal(kind, identity, cachedPid);
                return (ResponseStatus.DeadLetter, default)!;
            }
            catch (TimeoutException)
            {
                if (_requestLogThrottle().IsOpen())
                {
                    _logger.LogWarning("TryRequestAsync timed out, PID from {Source}", source);
                }
            }
            catch (Exception x)
            {
                _logger.LogWarning(x, "TryRequestAsync failed with exception, PID from {Source}", source);
            }

            _pidCache.RemoveByVal(kind, identity, cachedPid);

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