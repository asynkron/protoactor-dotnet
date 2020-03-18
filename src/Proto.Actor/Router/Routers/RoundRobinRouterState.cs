// -----------------------------------------------------------------------
//   <copyright file="RoundRobinRouterState.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Proto.Router.Routers
{
    class RoundRobinRouterState : RouterState
    {
        private int _currentIndex;
        private HashSet<PID> _routees;
        private List<PID> _values;
        private readonly ISenderContext _senderContext;

        internal RoundRobinRouterState(ISenderContext senderContext) => _senderContext = senderContext;

        public override HashSet<PID> GetRoutees() => _routees;

        public override void SetRoutees(HashSet<PID> routees)
        {
            _routees = routees;
            _values = routees.ToList();
        }

        public override void RouteMessage(object message)
        {
            var i = _currentIndex % _values.Count;
            var pid = _values[i];
            Interlocked.Add(ref _currentIndex, 1);
            _senderContext.Send(pid, message);
        }
    }
}