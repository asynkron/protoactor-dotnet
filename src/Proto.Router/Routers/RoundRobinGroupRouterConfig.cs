// -----------------------------------------------------------------------
//   <copyright file="RoundRobinGroupRouterConfig.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;

namespace Proto.Router.Routers
{
    class RoundRobinGroupRouterConfig : GroupRouterConfig
    {
        private readonly ActorSystem _system;

        public RoundRobinGroupRouterConfig(ActorSystem system, params PID[] routees)
        {
            _system = system;
            Routees = new HashSet<PID>(routees);
        }

        public override RouterState CreateRouterState() => new RoundRobinRouterState(_system);
    }
}