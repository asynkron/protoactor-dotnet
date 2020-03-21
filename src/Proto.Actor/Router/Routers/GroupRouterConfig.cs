// -----------------------------------------------------------------------
//   <copyright file="GroupRouterConfig.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;

namespace Proto.Router.Routers
{
    public abstract class GroupRouterConfig : RouterConfig
    {
        private readonly HashSet<PID> _routees;
        protected readonly ISenderContext SenderContext;

        protected GroupRouterConfig(ISenderContext senderContext, PID[] routees)
        {
            SenderContext = senderContext;
            _routees = new HashSet<PID>(routees);
        }

        public override void OnStarted(IContext context, RouterState router)
        {
            foreach (var pid in _routees)
            {
                context.Watch(pid);
            }
            router.SetRoutees(_routees);
        }
    }
}