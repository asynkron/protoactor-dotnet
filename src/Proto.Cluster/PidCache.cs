using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Proto.Router;
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
        public Task ReceiveAsync(IContext context)
        {
            throw new NotImplementedException();
        }
    }
}
