// -----------------------------------------------------------------------
//   <copyright file="RandomGroupRouterConfig.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;

namespace Proto.Router.Routers
{
    class RandomGroupRouterConfig : GroupRouterConfig
    {
        private readonly ISenderContext _senderContext;
        private readonly int? _seed;

        public RandomGroupRouterConfig(ISenderContext senderContext, int seed, params PID[] routees)
        {
            _senderContext = senderContext;
            _seed = seed;
            Routees = new HashSet<PID>(routees);
        }

        public RandomGroupRouterConfig(ISenderContext senderContext, params PID[] routees)
        {
            _senderContext = senderContext;
            Routees = new HashSet<PID>(routees);
        }

        public override RouterState CreateRouterState() => new RandomRouterState(_senderContext, _seed);
    }
}