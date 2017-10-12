using System.Linq;
using System.Threading.Tasks;
using Proto.Mailbox;

namespace Proto.Cluster.Router
{
    public class ClusterRouter
    {
        public static PID NewClusterBroadcastPool(string name, int poolSize, string kind)
        {
            var props = Actor.FromProducer(() => new ClusterBroadcastPool(name, poolSize, kind));
            var pid = Actor.Spawn(props);
            return pid;
        }
    }

    //TODO: There are two approaches that could be viable for cluster routers
    //this approach generates a name per routee and simply use Cluster.GetAsync on each
    //This works, but, this is not guaranteed to place routees evenly on different nodes.
    //
    //another approach could be to subscribe to cluster members and place actors on each of those
    //IMO, both are correct in their own ways
    public class ClusterBroadcastPool : IActor
    {
        private readonly string[] _routees;
        private readonly string _kind;

        public ClusterBroadcastPool(string name,int poolSize, string kind)
        {
            _kind = kind;
            _routees = Enumerable
                .Range(0, poolSize)
                .Select(i => name + i) //TODO: How should routees be named?
                .ToArray();
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case AutoReceiveMessage _:
                case SystemMessage _:
                    break;
                default:
                    var m = context.Message;
                    foreach (var routee in _routees)
                    {
#pragma warning disable 4014
                        Cluster.GetAsync(routee, _kind).ContinueWith(t =>
#pragma warning restore 4014
                        {
                            var pid = t.Result.Item1;
                            pid?.Tell(m);
                            //TODO: what to do if pid is null? (unavailable) 
                        });
                    }
                    break;
            }
            return Actor.Done;
        }
    }
}
