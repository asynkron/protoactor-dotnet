// -----------------------------------------------------------------------
//   <copyright file="PoolRouterConfig.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

namespace Proto.Router.Routers
{
    internal abstract record PoolRouterConfig(int PoolSize, Props RouteeProps) : RouterConfig
    {


        public override void OnStarted(IContext context, RouterState router)
        {
            var routees = Enumerable.Range(0, PoolSize).Select(x => context.Spawn(RouteeProps));
            router.SetRoutees(new HashSet<PID>(routees));
        }
    }
}