// -----------------------------------------------------------------------
//   <copyright file="BroadcastPoolRouterConfig.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

namespace Proto.Router.Routers
{
    class BroadcastPoolRouterConfig : PoolRouterConfig
    {
        private readonly ActorSystem _system;

        public BroadcastPoolRouterConfig(ActorSystem system, int poolSize, Props routeeProps)
            : base(poolSize, routeeProps)
        {
            _system = system;
        }

        public override RouterState CreateRouterState() => new BroadcastRouterState(_system);
    }
}