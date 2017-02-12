// -----------------------------------------------------------------------
//  <copyright file="ConsistentHashPoolRouterConfig.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

namespace Proto.Router.Routers
{
    internal class ConsistentHashPoolRouterConfig : PoolRouterConfig
    {
        public ConsistentHashPoolRouterConfig(int poolSize)
            : base(poolSize)
        {
        }

        public override RouterState CreateRouterState()
        {
            return new ConsistentHashRouterState();
        }
    }
}