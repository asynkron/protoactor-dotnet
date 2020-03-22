// -----------------------------------------------------------------------
//   <copyright file="RandomGroupRouterConfig.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

namespace Proto.Router.Routers
{
    class RandomGroupRouterConfig : GroupRouterConfig
    {
        private readonly int? _seed;

        public RandomGroupRouterConfig(ISenderContext senderContext, int seed, params PID[] routees) : base(senderContext, routees) => _seed = seed;

        public RandomGroupRouterConfig(ISenderContext senderContext, params PID[] routees) : base(senderContext, routees) { }

        public override RouterState CreateRouterState() => new RandomRouterState(SenderContext, _seed);
    }
}
