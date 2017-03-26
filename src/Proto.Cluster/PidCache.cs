// -----------------------------------------------------------------------
//   <copyright file="PidCache.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Proto.Remote;

namespace Proto.Cluster
{
    public static class PidCache
    {
        public static PID PID { get; private set; }

        public static void Spawn()
        {
            var props = Router.Router.NewConsistentHashPool(Actor.FromProducer(() => new PidCachePartitionActor()), 128);
            PID = Actor.SpawnNamed(props,"pidcache");
        }
    }

    public class PidCacheRequest : Router.IHashable
    {
        public PidCacheRequest(string name, string kind)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        }

        public string Name { get;  }
        public string Kind { get;  }
        public string HashBy()
        {
            return Name;
        }
    }
    public class PidCachePartitionActor : IActor
    {
        private readonly Dictionary<string,PID> _cache = new Dictionary<string, PID>();
        private readonly Dictionary<string,string> _reverseCache = new Dictionary<string, string>();

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                  //  Console.WriteLine("Started PidCacheActor");
                    break;
                case PidCacheRequest msg:
                    GetPid(context, msg);
                    break;
                case Terminated msg:
                    RemoveTerminated(msg);
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
                    var respid = res.Pid;
                    var key = respid.ToShortString();
                    _cache[name] = respid;
                    _reverseCache[key] = name;
                    context.Watch(respid);
                    context.Respond(res);
                    return Actor.Done;
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
