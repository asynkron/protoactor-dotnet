// -----------------------------------------------------------------------
// <copyright file="RandomGroupRouterConfig.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Proto.Router.Routers
{
    record RandomGroupRouterConfig : GroupRouterConfig
    {
        private readonly int _seed;

        public RandomGroupRouterConfig(ISenderContext senderContext, int seed, params PID[] routees) : base(
            senderContext, routees
        ) => _seed = seed;

        public RandomGroupRouterConfig(ISenderContext senderContext, params PID[] routees) : base(senderContext, routees
        )
        {
        }

        protected override RouterState CreateRouterState() => new RandomRouterState(SenderContext, _seed);
    }
}