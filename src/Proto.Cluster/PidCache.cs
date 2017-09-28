// -----------------------------------------------------------------------
//   <copyright file="PidCache.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Remote;
using Proto.Router;

namespace Proto.Cluster
{
    internal static class PidCache
    {
        //arbitrary value, number of partitions used in the PidCache.
        //the intention is just to reduce contention on too few actors when doing Pid lookups
        private const int PartitionCount = 100;

        internal static PID Pid { get; private set; }

        private static Subscription<object> clusterTopologyEvnSub;

        internal static void SubscribeToEventStream()
        {
            clusterTopologyEvnSub = Actor.EventStream.Subscribe<MemberStatusEvent>(Pid.Tell);
        }

        internal static void UnsubEventStream()
        {
            Actor.EventStream.Unsubscribe(clusterTopologyEvnSub.Id);
        }

        internal static void Spawn()
        {
            var props = Actor.FromProducer(() => new PidCachePartitionActor());
            Pid = Actor.SpawnNamed(props, "pidcache");
        }

        internal static void Stop()
        {
            Pid.Stop();
        }
    }

    internal class RemovePidCacheRequest : IHashable
    {
        public string Name { get; }

        public RemovePidCacheRequest(string name) => Name = name;

        public string HashBy() => Name;
    }
    
    internal class PidCacheRequest : IHashable
    {
        public PidCacheRequest(string name, string kind)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        }

        public string Name { get; }
        public string Kind { get; }

        public string HashBy()
        {
            return Name;
        }
    }

    internal class PidCacheResponse
    {
        public PID Pid { get; }
        public ResponseStatusCode StatusCode { get; }

        public PidCacheResponse(PID pid, ResponseStatusCode statusCode)
        {
            Pid = pid;
            StatusCode = statusCode;
        }
    }

    internal class PidCachePartitionActor : IActor
    {
        private readonly Dictionary<string, PID> _cache = new Dictionary<string, PID>();
        private readonly ILogger _logger = Log.CreateLogger<PidCachePartitionActor>();
        private readonly Dictionary<string, string> _reverseCache = new Dictionary<string, string>();
        private readonly Dictionary<string, HashSet<string>> _reverseCacheByMemberAddress = new Dictionary<string, HashSet<string>>();
        
        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    _logger.LogDebug("Started PidCacheActor");
                    break;
                case PidCacheRequest msg:
                    GetPid(context, msg);
                    break;
                case Terminated msg:
                    RemoveTerminated(msg);
                    break;
                case MemberLeftEvent _:
                case MemberRejoinedEvent _:
                    ClearCacheByMemberAddress(((MemberStatusEvent)context.Message).Address);
                    break;
            }
            return Actor.Done;
        }

        private void GetPid(IContext context, PidCacheRequest msg)
        {
            if (_cache.TryGetValue(msg.Name, out var pid))
            {
                //found the pid, replied, exit
                context.Respond(new PidCacheResponse(pid, ResponseStatusCode.OK));
                return;
            }

            var name = msg.Name;
            var kind = msg.Kind;

            context.ReenterAfter(MemberList.GetMemberAsync(name, kind), address =>
            {
                if (string.IsNullOrEmpty(address.Result))
                {
                    context.Respond(new PidCacheResponse(null, ResponseStatusCode.Unavailable));
                    return Actor.Done;
                }

                var remotePid = Partition.PartitionForKind(address.Result, kind);
                var req = new ActorPidRequest
                {
                    Kind = kind,
                    Name = name
                };
                var reqTask = remotePid.RequestAsync<ActorPidResponse>(req);
                context.ReenterAfter(reqTask, t =>
                {
                    var res = t.Result;
                    var status = (ResponseStatusCode) res.StatusCode;
                    switch (status)
                    {
                        case ResponseStatusCode.OK:
                            var key = res.Pid.ToShortString();
                            _cache[name] = res.Pid;
                            _reverseCache[key] = name;
                            if (_reverseCacheByMemberAddress.ContainsKey(res.Pid.Address))
                                _reverseCacheByMemberAddress[res.Pid.Address].Add(key);
                            else
                                _reverseCacheByMemberAddress[res.Pid.Address] = new HashSet<string> {key};

                            context.Watch(res.Pid);
                            context.Respond(new PidCacheResponse(res.Pid, status));
                            break;
                        default:
                            context.Respond(new PidCacheResponse(res.Pid, status));
                            break;
                    }
                    return Actor.Done;
                });
                return Actor.Done;
            });
        }

        private void ClearCacheByMemberAddress(string memberAddress)
        {
            if (_reverseCacheByMemberAddress.TryGetValue(memberAddress, out var keys))
            {
                foreach (var key in keys)
                {
                    if (_reverseCache.TryGetValue(key, out var name))
                    {
                        _reverseCache.Remove(key);
                        _cache.Remove(name);
                    }
                }
                _reverseCacheByMemberAddress.Remove(memberAddress);
                _logger.LogDebug("PidCache cleared cache by member address " + memberAddress);
            }
        }
        
        private void RemoveTerminated(Terminated msg)
        {
            var key = msg.Who.ToShortString();
            if (_reverseCache.TryGetValue(key, out var name))
            {
                _reverseCache.Remove(key);
                _reverseCacheByMemberAddress[msg.Who.Address].Remove(key);
                _cache.Remove(name);
            }
        }
    }
}