// -----------------------------------------------------------------------
//   <copyright file="BroadcastGroupRouterConfig.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;

namespace Proto.Router.Routers
{
    internal class BroadcastGroupRouterConfig : GroupRouterConfig
    {
        public BroadcastGroupRouterConfig(params PID[] routees) => Routees = new HashSet<PID>(routees);

        public override RouterState CreateRouterState() => new BroadcastRouterState();
    }
}