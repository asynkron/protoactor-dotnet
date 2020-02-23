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
            switch (context.Message)
            {
                case Started _:
                    _config.OnStarted(context, _routerState);
                    _wg.Set();
                    break;
                case RouterAddRoutee addRoutee:
                {
                    var r = _routerState.GetRoutees();

                    if (r.Contains(addRoutee.PID))
                    {
                        return Actor.Done;
                    }

                    context.Watch(addRoutee.PID);
                    r.Add(addRoutee.PID);
                    _routerState.SetRoutees(r);
                    break;
                }
                case RouterRemoveRoutee removeRoutee:
                {
                    var r = _routerState.GetRoutees();

                    if (!r.Contains(removeRoutee.PID))
                    {
                        return Actor.Done;
                    }

                    context.Unwatch(removeRoutee.PID);
                    r.Remove(removeRoutee.PID);
                    _routerState.SetRoutees(r);
                    break;
                }
                case RouterBroadcastMessage broadcastMessage:
                {
                    foreach (var routee in _routerState.GetRoutees())
                    {
                        context.Request(routee, broadcastMessage.Message);
                    }

                    break;
                }
                case RouterGetRoutees _:
                {
                    var r = _routerState.GetRoutees().ToList();
                    context.Respond(new Routees(r));
                    break;
                }
            }

            return Actor.Done;
        }
    }
}