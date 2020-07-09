// -----------------------------------------------------------------------
//   <copyright file="PidCache.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Mailbox;

namespace Proto.Cluster
{
    public class PidCache
    {
        private PID _watcher = new PID();
        private Subscription<object>? _clusterTopologyEvnSub;

        private readonly ConcurrentDictionary<string, PID> _cache = new ConcurrentDictionary<string, PID>();
        private readonly ConcurrentDictionary<string, string> _reverseCache = new ConcurrentDictionary<string, string>();

        private Cluster Cluster { get; }

        internal PidCache(Cluster cluster) => Cluster = cluster;

        internal void Setup()
        {
            var props = Props.FromProducer(() => new PidCacheWatcher(this)).WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy);
            _watcher = Cluster.System.Root.SpawnNamed(props, "PidCacheWatcher");
            _clusterTopologyEvnSub = Cluster.System.EventStream.Subscribe<MemberStatusEvent>(OnMemberStatusEvent);
        }

        internal void Stop()
        {
            Cluster.System.Root.Stop(_watcher);
            Cluster.System.EventStream.Unsubscribe(_clusterTopologyEvnSub);
        }

        private void OnMemberStatusEvent(MemberStatusEvent evn)
        {
            switch (evn)
            {
                case MemberLeftEvent _:
                case MemberRejoinedEvent _:
                    RemoveCacheByMemberAddress(evn.Address);
                    break;
            }
        }

        internal bool TryGetCache(string name, out PID pid) => _cache.TryGetValue(name, out pid);

        internal bool TryAddCache(string name, PID pid)
        {
            if (!_cache.TryAdd(name, pid)) return false;

            var key = pid.ToShortString();
            _reverseCache.TryAdd(key, name);
            Cluster.System.Root.Send(_watcher, new WatchPidRequest(pid));
            return true;
        }

        internal void RemoveCacheByPid(PID pid)
        {
            var key = pid.ToShortString();

            if (_reverseCache.TryRemove(key, out var name))
            {
                _cache.TryRemove(name, out _);
            }
        }

        private void RemoveCacheByMemberAddress(string memberAddress)
        {
            foreach (var (name, pid) in _cache.ToArray())
            {
                if (pid.Address != memberAddress) continue;

                _cache.TryRemove(name, out _);
                var key = pid.ToShortString();
                _reverseCache.TryRemove(key, out _);
            }
        }

        internal void EnsureNewAddress(string kind)
        {
            foreach (var (name, pid) in _cache.ToArray())
            {
                var address = Cluster.MemberList.GetPartition(name, kind);

                if (address != pid.Address)
                {
                    RemoveCacheByPid(pid);
                    
                    if (pid.Address == Cluster.System.ProcessRegistry.Address)
                        Cluster.System.Root.Stop(pid);
                }
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
                    _logger.LogDebug("[PidCacheWatcher] Started watching {Actor}", msg.Pid);
                    break;
                case Terminated msg:
                    PidCache.RemoveCacheByPid(msg.Who);
                    _logger.LogDebug("[PidCacheWatcher] Removed from cache {Actor}", msg.Who);
                    break;
            }

            return Actor.Done;
        }
    }
}