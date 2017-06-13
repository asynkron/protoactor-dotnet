// -----------------------------------------------------------------------
//  <copyright file="GroupRouterConfig.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Proto.Router.Routers
{
    public abstract class GroupRouterConfig : IGroupRouterConfig
    {
        protected HashSet<PID> Routees;

        public virtual async Task OnStartedAsync(IContext context, Props props, RouterState router)
        {
            foreach (var pid in Routees)
            {
                await context.WatchAsync(pid);
            }
            router.SetRoutees(Routees);
        }

        public abstract RouterState CreateRouterState();
    }
}