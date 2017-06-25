// -----------------------------------------------------------------------
//   <copyright file="RandomPoolRouterConfig.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------
namespace Proto.Router.Routers
{
    internal class RandomPoolRouterConfig : PoolRouterConfig
    {
        private readonly int? _seed;

        public RandomPoolRouterConfig(int poolSize, int? seed)
            : base(poolSize)
        {
            _seed = seed;
        }

        public override RouterState CreateRouterState()
        {
            return new RandomRouterState(_seed);
        }
    }
}