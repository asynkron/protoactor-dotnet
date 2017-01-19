using System.Collections.Generic;

namespace Proto.Router.Routers
{
    internal class RoundRobinGroupRouterConfig : GroupRouterConfig
    {
        public RoundRobinGroupRouterConfig(params PID[] routees)
        {
            Routees = new HashSet<PID>(routees);
        }

        public override RouterState CreateRouterState()
        {
            return new RoundRobinRouterState();
        }
    }
}