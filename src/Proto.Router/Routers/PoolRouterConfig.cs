// -----------------------------------------------------------------------
//  <copyright file="PoolRouterConfig.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Proto.Router.Routers
{
    internal abstract class PoolRouterConfig : IPoolRouterConfig
    {
        private readonly int _poolSize;

        protected PoolRouterConfig(int poolSize)
        {
            _poolSize = poolSize;
        }

        public virtual Task OnStartedAsync(IContext context, Props props, RouterState router)
        {
            var routees = Enumerable.Range(0, _poolSize).Select(x => context.Spawn(props));
            router.SetRoutees(new HashSet<PID>(routees));
            return Actor.Done;
        }

        public abstract RouterState CreateRouterState();
    }
}