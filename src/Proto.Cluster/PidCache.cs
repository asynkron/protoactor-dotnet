// -----------------------------------------------------------------------
//   <copyright file="PidCache.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Remote;

namespace Proto.Cluster
{
    internal static class PidCache
    {
        private static PID watcher;
        private static Subscription<object> clusterTopologyEvnSub;

        private static readonly ConcurrentDictionary<string, PID> _cache = new ConcurrentDictionary<string, PID>();
        private static readonly ConcurrentDictionary<string, string> _reverseCache = new ConcurrentDictionary<string, string>();

        internal static void Setup()
        {
            var props = Actor.FromProducer(() => new PidCacheWatcher());
            watcher = Actor.SpawnNamed(props, "PidCacheWatcher");
            clusterTopologyEvnSub = Actor.EventStream.Subscribe<MemberStatusEvent>(OnMemberStatusEvent);
        }

        internal static void Stop()
        {
            watcher.Stop();
            Actor.EventStream.Unsubscribe(clusterTopologyEvnSub.Id);
        }

        internal static void OnMemberStatusEvent(MemberStatusEvent evn)
        {
            if (evn is MemberLeftEvent || evn is MemberRejoinedEvent)
            {
                RemoveCacheByMemberAddress(evn.Address);
            }
        }

        internal static bool TryGetCache(string name, out PID pid)
        {
            return _cache.TryGetValue(name, out pid);
        }

        internal static bool TryAddCache(string name, PID pid)
        {
            if (_cache.TryAdd(name, pid))
            {
                var key = pid.ToShortString();
                _reverseCache.TryAdd(key, name);
                watcher.Tell(new WatchPidRequest(pid));
                return true;
            }
            return false;
        }

        internal static void RemoveCacheByPid(PID pid)
        {
            var key = pid.ToShortString();
            if (_reverseCache.TryRemove(key, out var name))
            {
                _cache.TryRemove(name, out _);
            }
        }

        internal static void RemoveCacheByName(string name)
        {
            if(_cache.TryRemove(name, out var pid))
            {
                _reverseCache.TryRemove(pid.ToShortString(), out _);
            }
        }

        internal static void RemoveCacheByMemberAddress(string memberAddress)
        {
            foreach (var (name, pid) in _cache.ToArray())
            {
                if (pid.Address == memberAddress)
                {
                    _cache.TryRemove(name, out _);
                    _reverseCache.TryRemove(name, out _);
                }
            }
        }
    }

    internal class WatchPidRequest
    {
        internal PID Pid { get; }

        public WatchPidRequest(PID pid) { Pid = pid; }
    }

    internal class PidCacheWatcher : IActor
    {
        private readonly ILogger _logger = Log.CreateLogger<PidCacheWatcher>();

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    _logger.LogDebug("Started PidCacheWatcher");
                    break;
                case WatchPidRequest msg:
                    context.Watch(msg.Pid);
                    break;
                case Terminated msg:
                    PidCache.RemoveCacheByPid(msg.Who);
                    break;
            }
            return Actor.Done;
        }
    }
}