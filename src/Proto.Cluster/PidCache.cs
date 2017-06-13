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

namespace Proto.Cluster
{
    public static class PidCache
    {
        //arbitrary value, number of partitions used in the PidCache.
        //the intention is just to reduce contention on too few actors when doing Pid lookups
        private const int PartitionCount = 100;

        public static PID Pid { get; private set; }

        public static void Spawn()
        {
            var props = Router.Router.NewConsistentHashPool(Actor.FromProducer(() => new PidCachePartitionActor()), PartitionCount);
            Pid = Actor.SpawnNamed(props, "pidcache");
        }
    }

    public class PidCacheRequest : Router.IHashable
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

    public class PidCachePartitionActor : IActor
    {
        private readonly ILogger _logger = Log.CreateLogger<PidCachePartitionActor>();
        private readonly Dictionary<string, PID> _cache = new Dictionary<string, PID>();
        private readonly Dictionary<string, string> _reverseCache = new Dictionary<string, string>();

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    _logger.LogDebug("Started PidCacheActor");
                    break;
                case PidCacheRequest msg:
                    return GetPid(context, msg);
                case Terminated msg:
                    RemoveTerminated(msg);
                    break;
            }
            return Actor.Done;
        }

        private async Task GetPid(IContext context, PidCacheRequest msg)
        {
            if (_cache.TryGetValue(msg.Name, out var pid))
            {
                await context.RespondAsync(new ActorPidResponse
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
                context.ReenterAfter(resp, async t =>
                {
                    var res = t.Result;
                    var respid = res.Pid;
                    var key = respid.ToShortString();
                    _cache[name] = respid;
                    _reverseCache[key] = name;
                    await context.WatchAsync(respid);
                    await context.RespondAsync(res);
                });
                return Actor.Done;
            });
        }

        private void RemoveTerminated(Terminated msg)
        {
            var key = msg.Who.ToShortString();
            if (_reverseCache.TryGetValue(key, out var name))
            {
                _reverseCache.Remove(key);
                _cache.Remove(name);
            }
        }
    }
}