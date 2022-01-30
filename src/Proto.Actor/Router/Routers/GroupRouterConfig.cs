// -----------------------------------------------------------------------
// <copyright file="GroupRouterConfig.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Proto.Router.Routers
{
    public abstract record GroupRouterConfig(ISenderContext SenderContext, PID[] Routees) : RouterConfig
    {
        public override void OnStarted(IContext context, RouterState router)
        {
            foreach (var pid in Routees)
            {
                context.Watch(pid);
            }

            router.SetRoutees(Routees);
        }
    }
}