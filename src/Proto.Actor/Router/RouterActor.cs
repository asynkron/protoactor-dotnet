// -----------------------------------------------------------------------
// <copyright file="RouterActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading.Tasks;
using Proto.Router.Messages;
using Proto.Router.Routers;

namespace Proto.Router;

public class RouterActor : IActor
{
    private readonly RouterConfig _config;
    private readonly RouterState _routerState;
    private readonly RouterStartNotification _startNotification;

    public RouterActor(RouterConfig config, RouterState routerState, RouterStartNotification startNotification)
    {
        _config = config;
        _routerState = routerState;
        _startNotification = startNotification;
    }

    public Task ReceiveAsync(IContext context)
    {
        if (context.Message is Started)
        {
            try
            {
                _config.OnStarted(context, _routerState);
                _startNotification.NotifyStarted();
            }
            catch (Exception e)
            {
                _startNotification.NotifyFailed(e);
            }

            return Task.CompletedTask;
        }

        if (context.Message is RouterAddRoutee addRoutee)
        {
            var r = _routerState.GetRoutees();

            if (r.Contains(addRoutee.Pid))
            {
                return Task.CompletedTask;
            }

            context.Watch(addRoutee.Pid);
            r.Add(addRoutee.Pid);
            _routerState.SetRoutees(r.ToArray());

            return Task.CompletedTask;
        }

        if (context.Message is RouterRemoveRoutee removeRoutee)
        {
            var r = _routerState.GetRoutees();

            if (!r.Contains(removeRoutee.Pid))
            {
                return Task.CompletedTask;
            }

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