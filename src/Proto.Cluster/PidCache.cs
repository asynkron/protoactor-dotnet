// -----------------------------------------------------------------------
//   <copyright file="PidCache.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Remote;
using Proto.Router;

namespace Proto.Cluster
{
    internal static class PidCache
    {
        internal static PID WatcherPid { get; private set; }

        private static Subscription<object> clusterTopologyEvnSub;

        private static readonly Object _lock = new Object();
        private static readonly Dictionary<string, PID> _cache = new Dictionary<string, PID>();
        private static readonly Dictionary<string, string> _reverseCache = new Dictionary<string, string>();

        internal static void SubscribeToEventStream()
        {
            clusterTopologyEvnSub = Actor.EventStream.Subscribe<MemberStatusEvent>(OnMemberStatusEvent);
        }

        internal static void UnsubEventStream()
        {
            Actor.EventStream.Unsubscribe(clusterTopologyEvnSub.Id);
        }

        internal static void Spawn()
        {
            var props = Actor.FromProducer(() => new PidCacheWatcher());
            WatcherPid = Actor.SpawnNamed(props, "PidCacheWatcher");
        }

        internal static void Stop()
        {
            WatcherPid.Stop();
        }

        internal static async Task<(PID, ResponseStatusCode)> GetPidAsync(string name, string kind, CancellationToken ct)
        {
            //Check Cache
            if (TryGetCache(name, out var pid))
                return (pid, ResponseStatusCode.OK);

            //Get Pid
            var address = MemberList.GetPartition(name, kind);

            if (string.IsNullOrEmpty(address))
            {
                return (null, ResponseStatusCode.Unavailable);
            }

            var remotePid = Partition.PartitionForKind(address, kind);
            var req = new ActorPidRequest
            {
                Kind = kind,
                Name = name
            };

            try
            {
                var resp = await (ct == null || ct == CancellationToken.None
                           ? remotePid.RequestAsync<ActorPidResponse>(req, Cluster.cfg.TimeoutTimespan)
                           : remotePid.RequestAsync<ActorPidResponse>(req, ct));
                var status = (ResponseStatusCode) resp.StatusCode;
                switch (status)
                {
                    case ResponseStatusCode.OK:
                        AddCache(name, resp.Pid);
                        WatcherPid.Tell(new WatchPidRequest(resp.Pid));
                        return (resp.Pid, status);
                    default:
                        return (resp.Pid, status);
                }
            }
            catch(TimeoutException)
            {
                return (null, ResponseStatusCode.Timeout);
            }
            catch
            {
                return (null, ResponseStatusCode.Error);
            }
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
            lock(_lock)
            {
                return _cache.TryGetValue(name, out pid);
            }
        }

        internal static void AddCache(string name, PID pid)
        {
            lock(_lock)
            {
                var key = pid.ToShortString();
                _cache[name] = pid;
                _reverseCache[key] = name;
            }
        }

        internal static void RemoveCacheByPid(PID pid)
        {
            lock(_lock)
            {
                var key = pid.ToShortString();
                if (_reverseCache.TryGetValue(key, out var name))
                {
                    _reverseCache.Remove(key);
                    _cache.Remove(name);
                }
            }
        }

        internal static void RemoveCacheByName(string name)
        {
            lock(_lock)
            {
                if (_cache.TryGetValue(name, out var pid))
                {
                    _cache.Remove(name);
                    _reverseCache.Remove(pid.ToShortString());
                }
            }
        }

        internal static void RemoveCacheByMemberAddress(string memberAddress)
        {
            lock(_lock)
            {
                foreach (var (name, pid) in _cache.ToArray())
                {
                    if (pid.Address == memberAddress)
                    {
                        _cache.Remove(name);
                        _reverseCache.Remove(pid.ToShortString());
                    }
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