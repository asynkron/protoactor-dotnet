// -----------------------------------------------------------------------
//   <copyright file="RoundRobinGroupRouterConfig.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

namespace Proto.Router.Routers
{
    internal class RoundRobinGroupRouterConfig : GroupRouterConfig
    {
        public RoundRobinGroupRouterConfig(ISenderContext senderContext, params PID[] routees) : base(senderContext,
            routees
        )
        {
        }

        public override RouterState CreateRouterState() => new RoundRobinRouterState(SenderContext);
    }
}