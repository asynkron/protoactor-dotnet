// -----------------------------------------------------------------------
//  <copyright file="GroupRouterConfig.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;

namespace Proto.Router.Routers
{
    public abstract class GroupRouterConfig : IGroupRouterConfig
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
}