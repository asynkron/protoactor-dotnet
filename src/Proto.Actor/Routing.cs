// -----------------------------------------------------------------------
//  <copyright file="Routing.cs" company="Asynkron HB">
//      Copyright (C) 2015-2016 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Proto
{
    public interface IRouterConfig
    {
        void OnStarted(IContext context, Props props, RouterState router);

        RouterState CreateRouterState();
    }

    public interface IGroupRouterConfig : IRouterConfig
    {
    }

    public interface IPoolRouterComfig : IRouterConfig
    {
    }

    public abstract class RouterState
    {
        public void SetRoutees(HashSet<PID> routees)
        {
            throw new NotImplementedException();
        }

        public void RouteMessage(object message, PID sender)
        {
            throw new NotImplementedException();
        }
    }

    public abstract class GroupRouter : IRouterConfig
    {
        protected HashSet<PID> Routees;

        public virtual void OnStarted(IContext context, Props props, RouterState router)
        {
            foreach (var pid in Routees)
            {
                context.Watch(pid);
            }
            router.SetRoutees(Routees);
        }

        public abstract RouterState CreateRouterState();
    }


    public abstract class PoolRouter : IRouterConfig
    {
        protected int PoolSize;
        public abstract void OnStarted(IContext context, Props props, RouterState router);
        public abstract RouterState CreateRouterState();
    }

    public abstract class RouterManagementMessage
    {
    }

    public class RouterActorRef : ActorRef
    {
        private readonly PID _router;
        private readonly RouterState _state;

        public RouterActorRef(PID router, RouterState state)
        {
            _router = router;
            _state = state;
        }

        public override void SendUserMessage(PID pid, object message, PID sender)
        {
            if (message is RouterManagementMessage)
            {
                var router = ProcessRegistry.Instance.Get(_router);
                router.SendUserMessage(pid, message, sender);
            }
            else
            {
                _state.RouteMessage(message, sender);
            }
        }

        public override void SendSystemMessage(PID pid, SystemMessage sys)
        {
            _router.SendSystemMessage(sys);
        }
    }
}