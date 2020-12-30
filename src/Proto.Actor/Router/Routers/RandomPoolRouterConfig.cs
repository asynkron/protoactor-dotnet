// -----------------------------------------------------------------------
// <copyright file="RandomPoolRouterConfig.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Proto.Router.Routers
{
    record RandomPoolRouterConfig : PoolRouterConfig
    {
        private readonly int? _seed;
        private readonly ISenderContext _senderContext;

        public RandomPoolRouterConfig(ISenderContext senderContext, int poolSize, Props routeeProps, int? seed)
            : base(poolSize, routeeProps)
        {
            _senderContext = senderContext;
            _seed = seed;
        }

        protected override RouterState CreateRouterState() => new RandomRouterState(_senderContext, _seed);
    }
}