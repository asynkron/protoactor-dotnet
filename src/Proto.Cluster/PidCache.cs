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
    internal static class PidCache
    {
        private static PID _watcher;
        private static Subscription<object> _clusterTopologyEvnSub;

        private static readonly ConcurrentDictionary<string, PID> Cache = new ConcurrentDictionary<string, PID>();
        private static readonly ConcurrentDictionary<string, string> ReverseCache = new ConcurrentDictionary<string, string>();

        internal static void Setup()
        {
            var props = Props.FromProducer(() => new PidCacheWatcher()).WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy);
            _watcher = RootContext.Empty.SpawnNamed(props, "PidCacheWatcher");
            _clusterTopologyEvnSub = Actor.EventStream.Subscribe<MemberStatusEvent>(OnMemberStatusEvent);
        }

        internal static void Stop()
        {
            _watcher.Stop();
            Actor.EventStream.Unsubscribe(_clusterTopologyEvnSub.Id);
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
            return Cache.TryGetValue(name, out pid);
        }

        internal static bool TryAddCache(string name, PID pid)
        {
            if (Cache.TryAdd(name, pid))
            {
                var key = pid.ToShortString();
                ReverseCache.TryAdd(key, name);
                RootContext.Empty.Send(_watcher, new WatchPidRequest(pid));
                return true;
            }
            return false;
        }

        internal static void RemoveCacheByPid(PID pid)
        {
            var key = pid.ToShortString();
            if (ReverseCache.TryRemove(key, out var name))
            {
                Cache.TryRemove(name, out _);
            }
        }

        internal static void RemoveCacheByName(string name)
        {
            if(Cache.TryRemove(name, out var pid))
            {
                ReverseCache.TryRemove(pid.ToShortString(), out _);
            }
        }

        internal static void RemoveCacheByMemberAddress(string memberAddress)
        {
            foreach (var (name, pid) in Cache.ToArray())
            {
                if (pid.Address == memberAddress)
                {
                    Cache.TryRemove(name, out _);
                    var key = pid.ToShortString();
                    ReverseCache.TryRemove(key, out _);
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