// -----------------------------------------------------------------------
//   <copyright file="RandomGroupRouterConfig.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;

namespace Proto.Router.Routers
{
    internal class RandomGroupRouterConfig : GroupRouterConfig
    {
        private readonly int? _seed;

        public RandomGroupRouterConfig(int seed, params PID[] routees)
        {
            _seed = seed;
            Routees = new HashSet<PID>(routees);
        }

        public RandomGroupRouterConfig(params PID[] routees)
        {
            Routees = new HashSet<PID>(routees);
        }

        public override RouterState CreateRouterState()
        {
            return new RandomRouterState(_seed);
        }
    }
}