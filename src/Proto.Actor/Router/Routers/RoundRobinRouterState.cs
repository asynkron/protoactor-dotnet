// -----------------------------------------------------------------------
//   <copyright file="RoundRobinRouterState.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Threading;

namespace Proto.Router.Routers
{
    class RoundRobinRouterState : RouterState
    {
        private int _currentIndex;
        private readonly ISenderContext _senderContext;

        internal RoundRobinRouterState(ISenderContext senderContext) => _senderContext = senderContext;

        public override void RouteMessage(object message)
        {
            var values = GetValues();
            var i = _currentIndex % values.Count;
            var pid = values[i];
            Interlocked.Add(ref _currentIndex, 1);
            _senderContext.Send(pid, message);
        }
    }
}
