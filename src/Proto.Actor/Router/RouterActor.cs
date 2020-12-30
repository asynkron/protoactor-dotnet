// -----------------------------------------------------------------------
// <copyright file="RouterActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
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
                return Task.CompletedTask;
            }

            if (context.Message is RouterAddRoutee addRoutee)
            {
                var r = _routerState.GetRoutees();
                if (r.Contains(addRoutee.Pid)) return Task.CompletedTask;

                context.Watch(addRoutee.Pid);
                r.Add(addRoutee.Pid);
                _routerState.SetRoutees(r.ToArray());
                return Task.CompletedTask;
            }

            if (context.Message is RouterRemoveRoutee removeRoutee)
            {
                var r = _routerState.GetRoutees();
                if (!r.Contains(removeRoutee.Pid)) return Task.CompletedTask;

                context.Unwatch(removeRoutee.Pid);
                r.Remove(removeRoutee.Pid);
                _routerState.SetRoutees(r.ToArray());
                return Task.CompletedTask;
            }

            if (context.Message is RouterBroadcastMessage broadcastMessage)
            {
                var sender = context.Sender;

                foreach (var routee in _routerState.GetRoutees())
                {
                    context.Request(routee, broadcastMessage.Message, sender);
                }

                return Task.CompletedTask;
            }

            if (context.Message is RouterGetRoutees)
            {
                var r = _routerState.GetRoutees().ToList();
                context.Respond(new Routees(r));
            }

            return Task.CompletedTask;
        }
    }
}