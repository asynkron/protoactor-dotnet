// -----------------------------------------------------------------------
// <copyright file="RoundRobinPoolRouterConfig.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Proto.Router.Routers
{
    record RoundRobinPoolRouterConfig : PoolRouterConfig
    {
        private readonly ISenderContext _senderContext;

        public RoundRobinPoolRouterConfig(ISenderContext senderContext, int poolSize, Props routeeProps)
            : base(poolSize, routeeProps) => _senderContext = senderContext;

        protected override RouterState CreateRouterState() => new RoundRobinRouterState(_senderContext);
    }
}