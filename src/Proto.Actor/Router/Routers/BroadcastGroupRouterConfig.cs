// -----------------------------------------------------------------------
//   <copyright file="BroadcastGroupRouterConfig.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

namespace Proto.Router.Routers
{
    class BroadcastGroupRouterConfig : GroupRouterConfig
    {
        public BroadcastGroupRouterConfig(ISenderContext senderContext, params PID[] routees) : base(senderContext, routees) { }

        public override RouterState CreateRouterState() => new BroadcastRouterState(SenderContext);
    }
}
