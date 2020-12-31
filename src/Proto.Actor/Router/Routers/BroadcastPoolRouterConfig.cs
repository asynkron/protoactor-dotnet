// -----------------------------------------------------------------------
// <copyright file="BroadcastPoolRouterConfig.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Proto.Router.Routers
{
    record BroadcastPoolRouterConfig : PoolRouterConfig
    {
        private readonly ISenderContext _senderContext;

        public BroadcastPoolRouterConfig(ISenderContext senderContext, int poolSize, Props routeeProps)
            : base(poolSize, routeeProps) => _senderContext = senderContext;

        protected override RouterState CreateRouterState() => new BroadcastRouterState(_senderContext);
    }
}