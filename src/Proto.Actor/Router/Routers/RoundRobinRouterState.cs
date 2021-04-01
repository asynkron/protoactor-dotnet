// -----------------------------------------------------------------------
// <copyright file="RoundRobinRouterState.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;
using System.Threading;

namespace Proto.Router.Routers
{
    internal class RoundRobinRouterState : RouterState
    {
        private readonly ISenderContext _senderContext;
        private int _currentIndex;

        internal RoundRobinRouterState(ISenderContext senderContext) => _senderContext = senderContext;

        public override void RouteMessage(object message)
        {
            ImmutableList<PID>? values = GetValues();
            int i = _currentIndex % values.Count;
            PID? pid = values[i];
            Interlocked.Add(ref _currentIndex, 1);
            _senderContext.Send(pid, message);
        }
    }
}
