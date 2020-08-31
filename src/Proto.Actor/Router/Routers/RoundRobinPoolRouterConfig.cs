// -----------------------------------------------------------------------
//   <copyright file="RoundRobinPoolRouterConfig.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

namespace Proto.Router.Routers
{
    internal class RoundRobinPoolRouterConfig : PoolRouterConfig
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