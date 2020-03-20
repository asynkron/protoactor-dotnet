// -----------------------------------------------------------------------
//   <copyright file="BroadcastPoolRouterConfig.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

namespace Proto.Router.Routers
{
    class BroadcastPoolRouterConfig : PoolRouterConfig
    {
        private readonly ISenderContext _senderContext;

        public BroadcastPoolRouterConfig(ISenderContext senderContext, int poolSize, Props routeeProps)
            : base(poolSize, routeeProps)
        {
            _senderContext = senderContext;
        }

        public override RouterState CreateRouterState() => new BroadcastRouterState(_senderContext);
    }
}