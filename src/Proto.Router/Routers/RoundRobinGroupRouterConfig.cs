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
        private readonly ISenderContext _senderContext;

        public RoundRobinGroupRouterConfig(ISenderContext senderContext, params PID[] routees)
        {
            _senderContext = senderContext;
            Routees = new HashSet<PID>(routees);
        }

        public override RouterState CreateRouterState() => new RoundRobinRouterState(_senderContext);
    }
}