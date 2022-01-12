﻿// -----------------------------------------------------------------------
// <copyright file="RoundRobinRouterState.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading;

namespace Proto.Router.Routers
{
    class RoundRobinRouterState : RouterState
    {
        private readonly ISenderContext _senderContext;
        private int _currentIndex;

        internal RoundRobinRouterState(ISenderContext senderContext) => _senderContext = senderContext;

        public override void RouteMessage(object message)
        {
            var values = GetValues();
            var i = Interlocked.Increment(ref _currentIndex) % values.Count;
            var pid = values[i];
            _senderContext.Send(pid, message);
        }
    }
}