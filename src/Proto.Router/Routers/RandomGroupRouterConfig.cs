using System.Collections.Generic;

namespace Proto.Router.Routers
{
    internal class RandomGroupRouterConfig : GroupRouterConfig
    {
        public RandomGroupRouterConfig(params PID[] routees)
        {
            Routees = new HashSet<PID>(routees);
        }

        public override RouterState CreateRouterState()
        {
            return new RandomRouterState();
        }
    }
}