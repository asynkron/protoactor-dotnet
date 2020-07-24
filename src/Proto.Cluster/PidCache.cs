// -----------------------------------------------------------------------
//   <copyright file="PidCache.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster
{
    internal class PidCache
    {
        private readonly ConcurrentDictionary<string, PID> _cacheIdentityToPid = new ConcurrentDictionary<string, PID>();
        private readonly ConcurrentDictionary<string, string> _cachePidToIdentity = new ConcurrentDictionary<string, string>();

        internal void OnMemberStatusEvent(MemberStatusEvent evn)
        {
            if (evn is MemberLeftEvent || evn is MemberRejoinedEvent)
            {
                RemoveByMemberAddress(evn.Address);
            }
        }

        internal bool TryGetCache(string identity, out PID pid) => _cacheIdentityToPid.TryGetValue(identity, out pid);

        internal bool TryAddCache(string identity, PID pid)
        {
            if (!_cacheIdentityToPid.TryAdd(identity, pid)) return false;

            var key = pid.ToShortString();
            _cachePidToIdentity.TryAdd(key, identity);
            return true;
        }

        internal void RemoveByPid(PID pid)
        {
            var key = pid.ToShortString();

            if (_cachePidToIdentity.TryRemove(key, out var identity))
            {
                _cacheIdentityToPid.TryRemove(identity, out _);
            }
        }

        private void RemoveByMemberAddress(string memberAddress)
        {
            foreach (var (identity, pid) in _cacheIdentityToPid.ToArray())
            {
                if (pid.Address != memberAddress)
                {
                    continue;
                }

                _cacheIdentityToPid.TryRemove(identity, out _);
                var key = pid.ToShortString();
                _cachePidToIdentity.TryRemove(key, out _);
            }
        }
    }

    class WatchPidRequest
    {
        internal PID Pid { get; }

        public WatchPidRequest(PID pid) => Pid = pid;
    }

    class PidCacheWatcher : IActor
    {
        private readonly ILogger _logger = Log.CreateLogger<PidCacheWatcher>();
        
        public PidCacheWatcher(PidCache pidCache) => PidCache = pidCache;

        private PidCache PidCache { get; }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    _logger.LogDebug("[PidCacheWatcher] Started");
                    break;
                case WatchPidRequest msg:
                    context.Watch(msg.Pid);
                    break;
                case Terminated msg:
                    PidCache.RemoveByPid(msg.Who);
                    break;
            }

            return Actor.Done;
        }
    }
}