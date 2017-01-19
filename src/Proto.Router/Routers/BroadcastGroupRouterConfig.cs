using System.Collections.Generic;

namespace Proto.Router.Routers
{
    internal class BroadcastGroupRouterConfig : GroupRouterConfig
    {
        public BroadcastGroupRouterConfig(params PID[] routees)
        {
            Routees = new HashSet<PID>(routees);
        }

        public override RouterState CreateRouterState()
        {
            return new BroadcastRouterState();
        }
    }
}