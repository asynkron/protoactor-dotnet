// -----------------------------------------------------------------------
//   <copyright file="BroadcastGroupRouterConfig.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;

namespace Proto.Router.Routers
{
    class BroadcastGroupRouterConfig : GroupRouterConfig
    {
        private readonly ActorSystem _system;

        public BroadcastGroupRouterConfig(ActorSystem system, params PID[] routees)
        {
            Routees = new HashSet<PID>(routees);
            _system = system;
        }

        public override RouterState CreateRouterState() => new BroadcastRouterState(_system);
    }
}