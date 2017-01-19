using System.Collections.Generic;

namespace Proto.Routing.Routers
{
    internal class BroadcastRouterState : RouterState
    {
        private HashSet<PID> _routees;

        public override HashSet<PID> GetRoutees()
        {
            return _routees;
        }

        public override void SetRoutees(HashSet<PID> routees)
        {
            _routees = routees;
        }

        public override void RouteMessage(object message, PID sender)
        {
            foreach (var pid in _routees)
            {
                pid.Request(message, sender);
            }
        }
    }
}