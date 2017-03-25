// -----------------------------------------------------------------------
//   <copyright file="PidCache.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

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
            var props = Router.Router.NewConsistentHashPool(Actor.FromProducer(() => new PidCacheActor()), 128);
            PID = Actor.SpawnNamed(props,"pidcache");
        }
    }
    public class PidCacheActor : IActor
    {
        private readonly Dictionary<string,PID> _cache = new Dictionary<string, PID>();
        private readonly Dictionary<string,string> _reverseCache = new Dictionary<string, string>();

        public async Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case ActorPidRequest msg:
                    if (_cache.TryGetValue(msg.Name, out var pid))
                    {
                        context.Respond(new ActorPidResponse()
                                        {
                                             Pid = pid,
                                        });
                        return;
                    }

                    var name = msg.Name;
                    var kind = msg.Kind;

                    var address = MemberList.GetMember(name, kind);
                    var remotePid = Partition.PartitionForKind(address, kind);
                    var req = new ActorPidRequest()
                              {
                                  Kind = kind,
                                  Name = name,
                              };
                    var resp = remotePid.RequestAsync<ActorPidResponse>(req);
                    context.AwaitTask(resp, t =>
                    {
                        var res = t.Result;
                        var respid = res.Pid;
                        _cache[name] = respid;
                        _reverseCache[respid.ToShortString()] = name;
                        context.Watch(respid);
                        context.Respond(res);
                        return Actor.Done;
                    });
                    break;
            }
        }
    }
}
