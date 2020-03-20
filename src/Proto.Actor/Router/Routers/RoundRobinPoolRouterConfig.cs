// -----------------------------------------------------------------------
//   <copyright file="RoundRobinPoolRouterConfig.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

namespace Proto.Router.Routers
{
    class RoundRobinPoolRouterConfig : PoolRouterConfig
    {
        private readonly ISenderContext _senderContext;

        public RoundRobinPoolRouterConfig(ISenderContext senderContext, int poolSize, Props routeeProps)
            : base(poolSize, routeeProps)
        {
            _senderContext = senderContext;
        }

        public override RouterState CreateRouterState() => new RoundRobinRouterState(_senderContext);
    }
}