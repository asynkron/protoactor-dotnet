// -----------------------------------------------------------------------
//   <copyright file="BroadcastRouterState.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;

namespace Proto.Router.Routers
{
    class BroadcastRouterState : RouterState
    {
        private HashSet<PID> _routees;
        private readonly ISenderContext _senderContext;

        internal BroadcastRouterState(ISenderContext senderContext)
        {
            _senderContext = senderContext;
        }

        public override HashSet<PID> GetRoutees() => _routees;

        public override void SetRoutees(HashSet<PID> routees) => _routees = routees;

        public override void RouteMessage(object message)
        {
            foreach (var pid in _routees)
            {
                _senderContext.Send(pid, message);
            }
        }
    }
}