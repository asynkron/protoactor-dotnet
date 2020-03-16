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
        private readonly ActorSystem _system;
        private readonly int? _seed;

        public RandomGroupRouterConfig(ActorSystem system, int seed, params PID[] routees)
        {
            _system = system;
            _seed = seed;
            Routees = new HashSet<PID>(routees);
        }

        public RandomGroupRouterConfig(ActorSystem system, params PID[] routees)
        {
            _system = system;
            Routees = new HashSet<PID>(routees);
        }

        public override RouterState CreateRouterState() => new RandomRouterState(_system, _seed);
    }
}