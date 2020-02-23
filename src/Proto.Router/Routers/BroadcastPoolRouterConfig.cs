// -----------------------------------------------------------------------
//   <copyright file="BroadcastPoolRouterConfig.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

namespace Proto.Router.Routers
{
    class BroadcastPoolRouterConfig : PoolRouterConfig
    {
        public BroadcastPoolRouterConfig(int poolSize, Props routeeProps)
            : base(poolSize, routeeProps) { }

        public override RouterState CreateRouterState() => new BroadcastRouterState();
    }
}