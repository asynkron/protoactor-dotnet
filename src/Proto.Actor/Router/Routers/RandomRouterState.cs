// -----------------------------------------------------------------------
// <copyright file="RandomRouterState.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;

namespace Proto.Router.Routers
{
    class RandomRouterState : RouterState
    {
        private readonly Random _random;
        private readonly ISenderContext _senderContext;

        public RandomRouterState(ISenderContext senderContext, int? seed)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
            _senderContext = senderContext;
        }

        public override void RouteMessage(object message)
        {
            var i = _random.Next(Values.Count);
            var pid = Values[i];
            _senderContext.Send(pid, message);
        }
    }
}