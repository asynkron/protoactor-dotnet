using System.Collections.Generic;

namespace Proto.Routing.Routers
{
    internal class ConsistentHashGroupRouterConfig : GroupRouterConfig
    {
        public ConsistentHashGroupRouterConfig(params PID[] routees)
        {
            Routees = new HashSet<PID>(routees);
        }

        public override RouterState CreateRouterState()
        {
            return new ConsistentHashRouterState();
        }
    }
}