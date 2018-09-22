// -----------------------------------------------------------------------
//   <copyright file="GroupRouterConfig.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;

namespace Proto.Router.Routers
{
    public abstract class GroupRouterConfig : RouterConfig
    {
        protected HashSet<PID> Routees;

        public override void OnStarted(IContext context, RouterState router)
        {
            foreach (var pid in Routees)
            {
                context.Watch(pid);
            }
            router.SetRoutees(Routees);
        }
    }
}