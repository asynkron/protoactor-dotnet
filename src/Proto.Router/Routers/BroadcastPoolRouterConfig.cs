// -----------------------------------------------------------------------
//  <copyright file="BroadcastPoolRouterConfig.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

namespace Proto.Router.Routers
{
    internal class BroadcastPoolRouterConfig : PoolRouterConfig
    {
        public BroadcastPoolRouterConfig(int poolSize)
            : base(poolSize)
        {
        }

        public override RouterState CreateRouterState()
        {
            return new BroadcastRouterState();
        }
    }
}