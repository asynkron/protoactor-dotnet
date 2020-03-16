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
    class PidCache
    {
        private PID watcher;
        private Subscription<object> clusterTopologyEvnSub;

        private readonly ConcurrentDictionary<string, PID> Cache = new ConcurrentDictionary<string, PID>();
        private readonly ConcurrentDictionary<string, string> ReverseCache = new ConcurrentDictionary<string, string>();

        public Cluster Cluster { get; }

        internal PidCache(Cluster cluster)
        {
            Cluster = cluster;
        }
        internal void Setup()
        {
            var props = Props.FromProducer(() => new PidCacheWatcher(this)).WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy);
            watcher = Cluster.System.Root.SpawnNamed(props, "PidCacheWatcher");
            clusterTopologyEvnSub = Cluster.System.EventStream.Subscribe<MemberStatusEvent>(OnMemberStatusEvent);
        }

        internal void Stop()
        {
            Cluster.System.Root.Stop(watcher);
            Cluster.System.EventStream.Unsubscribe(clusterTopologyEvnSub.Id);
        }

        private void OnMemberStatusEvent(MemberStatusEvent evn)
        {
            if (evn is MemberLeftEvent || evn is MemberRejoinedEvent)
            {
                RemoveCacheByMemberAddress(evn.Address);
            }
        }

        internal bool TryGetCache(string name, out PID pid) => Cache.TryGetValue(name, out pid);

        internal bool TryAddCache(string name, PID pid)
        {
            if (!Cache.TryAdd(name, pid)) return false;

            var key = pid.ToShortString();
            ReverseCache.TryAdd(key, name);
            Cluster.System.Root.Send(watcher, new WatchPidRequest(pid));
            return true;
        }

        internal void RemoveCacheByPid(PID pid)
        {
            var key = pid.ToShortString();

            if (ReverseCache.TryRemove(key, out var name))
            {
                Cache.TryRemove(name, out _);
            }
        }

        internal void RemoveCacheByName(string name)
        {
            if (Cache.TryRemove(name, out var pid))
            {
                ReverseCache.TryRemove(pid.ToShortString(), out _);
            }
        }

        private void RemoveCacheByMemberAddress(string memberAddress)
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

    class WatchPidRequest
    {
        internal PID Pid { get; }

        public WatchPidRequest(PID pid) => Pid = pid;
    }

    class PidCacheWatcher : IActor
    {
        private readonly ILogger _logger = Log.CreateLogger<PidCacheWatcher>();
        public PidCacheWatcher(PidCache pidCache)
        {
            PidCache = pidCache;
        }

        public PidCache PidCache { get; }

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