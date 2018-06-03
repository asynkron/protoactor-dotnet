// -----------------------------------------------------------------------
//   <copyright file="RoundRobinPoolRouterConfig.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------
namespace Proto.Router.Routers
{
    internal class RoundRobinPoolRouterConfig : PoolRouterConfig
    {
        public RoundRobinPoolRouterConfig(int poolSize, Props routeeProps)
            : base(poolSize,routeeProps)
        {
        }

        public override RouterState CreateRouterState() => new RoundRobinRouterState();
    }
}