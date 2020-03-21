// -----------------------------------------------------------------------
//   <copyright file="RoundRobinGroupRouterConfig.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

namespace Proto.Router.Routers
{
    class RoundRobinGroupRouterConfig : GroupRouterConfig
    {
        public RoundRobinGroupRouterConfig(ISenderContext senderContext, params PID[] routees) : base(senderContext, routees) { }

        public override RouterState CreateRouterState() => new RoundRobinRouterState(SenderContext);
    }
}
