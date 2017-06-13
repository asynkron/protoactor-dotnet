// -----------------------------------------------------------------------
//  <copyright file="RouterActor.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
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
        private readonly IRouterConfig _config;
        private readonly Props _routeeProps;
        private readonly RouterState _routerState;
        private readonly AutoResetEvent _wg;

        public RouterActor(Props routeeProps, IRouterConfig config, RouterState routerState, AutoResetEvent wg)
        {
            _routeeProps = routeeProps;
            _config = config;
            _routerState = routerState;
            _wg = wg;
        }

        public async Task ReceiveAsync(IContext context)
        {
            if (context.Message is Started)
            {
                await _config.OnStartedAsync(context, _routeeProps, _routerState);
                _wg.Set();
                return;
            }
            if (context.Message is RouterAddRoutee addRoutee)
            {
                var r = _routerState.GetRoutees();
                if (r.Contains(addRoutee.PID))
                {
                    return;
                }
                await context.WatchAsync(addRoutee.PID);
                r.Add(addRoutee.PID);
                _routerState.SetRoutees(r);
                return;
            }
            if (context.Message is RouterRemoveRoutee removeRoutee)
            {
                var r = _routerState.GetRoutees();
                if (!r.Contains(removeRoutee.PID))
                {
                    return;
                }
                await context.UnwatchAsync(removeRoutee.PID);
                r.Remove(removeRoutee.PID);
                _routerState.SetRoutees(r);
                return;
            }
            if (context.Message is RouterBroadcastMessage broadcastMessage)
            {
                foreach (var routee in _routerState.GetRoutees())
                {
                    await routee.RequestAsync(broadcastMessage.Message, context.Sender);
                }
            }
            if (context.Message is RouterGetRoutees)
            {
                var r = _routerState.GetRoutees().ToList();
                await context.Sender.SendAsync(new Routees(r));
            }
        }
    }
}