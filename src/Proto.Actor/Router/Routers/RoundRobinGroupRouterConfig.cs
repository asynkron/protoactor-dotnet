// -----------------------------------------------------------------------
// <copyright file="RoundRobinGroupRouterConfig.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

namespace Proto.Router.Routers;

internal record RoundRobinGroupRouterConfig : GroupRouterConfig
{
    public RoundRobinGroupRouterConfig(ISenderContext senderContext, params PID[] routees) : base(senderContext,
        routees
    )
    {
    }

    protected override RouterState CreateRouterState() => new RoundRobinRouterState(SenderContext);
}