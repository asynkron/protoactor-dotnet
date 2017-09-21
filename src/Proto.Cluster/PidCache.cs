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

    internal class RemoveCachedPidRequest
    {
        public string Name { get; }

        public RemoveCachedPidRequest(string name)
        {
            Name = name;
        }
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
                context.Respond(new ActorPidResponse
                {
                    Pid = pid
                });
                return; //found the pid, replied, exit
            }

            var name = msg.Name;
            var kind = msg.Kind;

            context.ReenterAfter(MemberList.GetMemberAsync(name, kind), address =>
            {
                var remotePid = Partition.PartitionForKind(address.Result, kind);
                var req = new ActorPidRequest
                {
                    Kind = kind,
                    Name = name
                };
                var resp = remotePid.RequestAsync<ActorPidResponse>(req);
                context.ReenterAfter(resp, t =>
                {
                    var res = t.Result;
                    switch ((ActorPidRequestStatusCode) res.StatusCode)
                    {
                        case ActorPidRequestStatusCode.OK:
                            var respid = res.Pid;
                            var key = respid.ToShortString();
                            _cache[name] = respid;
                            _reverseCache[key] = name;
                            if (_reverseCacheByMemberAddress.ContainsKey(respid.Address))
                                _reverseCacheByMemberAddress[respid.Address].Add(key);
                            else
                                _reverseCacheByMemberAddress[respid.Address] = new HashSet<string> {key};

                            context.Watch(respid);
                            context.Respond(res);
                            break;
                        default:
                            context.Respond(res);
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