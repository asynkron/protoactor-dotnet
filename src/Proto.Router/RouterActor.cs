// -----------------------------------------------------------------------
//   <copyright file="RouterActor.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Proto.Router.Messages;
using Proto.Router.Routers;

namespace Proto.Router
{
    public class RouterActor : IActor
    {
        private readonly RouterConfig _config;
        private readonly RouterState _routerState;
        private readonly AutoResetEvent _wg;

        public RouterActor(RouterConfig config, RouterState routerState, AutoResetEvent wg)
        {
            _config = config;
            _routerState = routerState;
            _wg = wg;
        }

        public Task ReceiveAsync(IContext context)
        {
            if (context.Message is Started)
            {
                _config.OnStarted(context, _routerState);
                _wg.Set();
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
                context.Sender.Send(new Routees(r));
            }
            return Actor.Done;
        }
    }
}