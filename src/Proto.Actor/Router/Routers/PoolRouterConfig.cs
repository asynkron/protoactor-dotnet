// -----------------------------------------------------------------------
// <copyright file="PoolRouterConfig.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Linq;

namespace Proto.Router.Routers
{
    abstract record PoolRouterConfig(int PoolSize, Props RouteeProps) : RouterConfig
    {
        public override void OnStarted(IContext context, RouterState router) =>
            router.SetRoutees(Enumerable
                .Range(0, PoolSize)
                .Select(_ => context.Spawn(RouteeProps))
                .ToArray()
            );
    }
}