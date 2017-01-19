using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Proto.Routing.Routers
{
    internal class RoundRobinRouterState : RouterState
    {
        private int _currentIndex;
        private HashSet<PID> _routees;
        private List<PID> _values;

        public override HashSet<PID> GetRoutees()
        {
            return _routees;
        }

        public override void SetRoutees(HashSet<PID> routees)
        {
            _routees = routees;
            _values = routees.ToList();
        }

        public override void RouteMessage(object message, PID sender)
        {
            var i = _currentIndex % _values.Count;
            var pid = _values[i];
            Interlocked.Add(ref _currentIndex, 1);
            pid.Request(message, sender);
        }
    }
}