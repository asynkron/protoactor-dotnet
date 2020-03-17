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
        private readonly ISenderContext _senderContext;

        public BroadcastGroupRouterConfig(ISenderContext senderContext, params PID[] routees)
        {
            Routees = new HashSet<PID>(routees);
            _senderContext = senderContext;
        }

        public override RouterState CreateRouterState() => new BroadcastRouterState(_senderContext);
    }
}