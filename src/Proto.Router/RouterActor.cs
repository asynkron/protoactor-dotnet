using System.Linq;
using System.Threading.Tasks;
using Proto.Router.Messages;
using Proto.Router.Routers;

namespace Proto.Router
{
    public class RouterActor : IActor
    {
        private readonly IRouterConfig _config;
        private readonly Props _routeeProps;
        private readonly RouterState _routerState;

        public RouterActor(Props routeeProps, IRouterConfig config, RouterState routerState)
        {
            _routeeProps = routeeProps;
            _config = config;
            _routerState = routerState;
        }

        public Task ReceiveAsync(IContext context)
        {
            if (context.Message is Started)
            {
                _config.OnStarted(context, _routeeProps, _routerState);
                return Actor.Done;
            }
            if (context.Message is RouterAddRoutee addRoutee)
            {
                var r = _routerState.GetRoutees();
                if (r.Contains(addRoutee.PID))
                {
                    return Actor.Done;
                }
                context.Watch(addRoutee.PID);
                r.Add(addRoutee.PID);
                _routerState.SetRoutees(r);
                return Actor.Done;
            }
            if (context.Message is RouterRemoveRoutee removeRoutee)
            {
                var r = _routerState.GetRoutees();
                if (!r.Contains(removeRoutee.PID))
                {
                    return Actor.Done;
                }
                context.Unwatch(removeRoutee.PID);
                r.Remove(removeRoutee.PID);
                _routerState.SetRoutees(r);
                return Actor.Done;
            }
            if (context.Message is RouterBroadcastMessage broadcastMessage)
            {
                foreach (var routee in _routerState.GetRoutees())
                {
                    routee.Request(broadcastMessage.Message, context.Sender);
                }
                return Actor.Done;
            }
            if (context.Message is RouterGetRoutees)
            {
                var r = _routerState.GetRoutees().ToList();
                context.Sender.Tell(new Routees(r));
            }
            return Actor.Done;
        }
    }
}