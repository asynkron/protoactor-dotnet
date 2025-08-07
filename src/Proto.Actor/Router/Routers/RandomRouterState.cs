// -----------------------------------------------------------------------
// <copyright file="RandomRouterState.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace Proto.Router.Routers;

internal class RandomRouterState : RouterState
{
    private readonly Random _random;
    private readonly ISenderContext _senderContext;

    public RandomRouterState(ISenderContext senderContext, int? seed)
    {
        // Use a single random source to avoid identical sequences across router states
        _random = seed.HasValue ? new Random(seed.Value) : Random.Shared;
        _senderContext = senderContext;
    }

    public override void RouteMessage(object message)
    {
        var i = _random.Next(Values.Count);
        var pid = Values[i];
        _senderContext.Send(pid, message);
    }
}