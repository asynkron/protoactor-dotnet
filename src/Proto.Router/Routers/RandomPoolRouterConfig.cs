// -----------------------------------------------------------------------
//   <copyright file="RandomPoolRouterConfig.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------
namespace Proto.Router.Routers
{
    class RandomPoolRouterConfig : PoolRouterConfig
    {
        private readonly ActorSystem _system;
        private readonly int? _seed;

        public RandomPoolRouterConfig(ActorSystem system, int poolSize, Props routeeProps, int? seed)
            : base(poolSize, routeeProps)
        {
            _system = system;
            _seed = seed;
        }

        public override RouterState CreateRouterState() => new RandomRouterState(_system, _seed);
    }
}