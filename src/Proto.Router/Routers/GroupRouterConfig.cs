using System.Collections.Generic;

namespace Proto.Router.Routers
{
    public abstract class GroupRouterConfig : IGroupRouterConfig
    {
        protected HashSet<PID> Routees;

        public virtual void OnStarted(IContext context, Props props, RouterState router)
        {
            foreach (var pid in Routees)
            {
                context.Watch(pid);
            }
            router.SetRoutees(Routees);
        }

        public abstract RouterState CreateRouterState();
    }
}