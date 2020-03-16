// -----------------------------------------------------------------------
//   <copyright file="RoundRobinPoolRouterConfig.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

namespace Proto.Router.Routers
{
    class RoundRobinPoolRouterConfig : PoolRouterConfig
    {
        private readonly ActorSystem _system;

        public RoundRobinPoolRouterConfig(ActorSystem system, int poolSize, Props routeeProps)
            : base(poolSize, routeeProps)
        {
            _system = system;
        }

        public override RouterState CreateRouterState() => new RoundRobinRouterState(_system);
    }
}