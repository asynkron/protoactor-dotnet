// -----------------------------------------------------------------------
//   <copyright file="RandomPoolRouterConfig.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------
namespace Proto.Router.Routers
{
    internal class RandomPoolRouterConfig : PoolRouterConfig
    {
        private readonly int? _seed;

        public RandomPoolRouterConfig(int poolSize, Props routeeProps, int? seed)
            : base(poolSize,routeeProps)
        {
            _seed = seed;
        }

        public override RouterState CreateRouterState() => new RandomRouterState(_seed);
    }
}