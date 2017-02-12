// -----------------------------------------------------------------------
//  <copyright file="PoolRouterConfig.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

namespace Proto.Router.Routers
{
    internal abstract class PoolRouterConfig : IPoolRouterConfig
    {
        private readonly int _poolSize;

        protected PoolRouterConfig(int poolSize)
        {
            _poolSize = poolSize;
        }

        public virtual void OnStarted(IContext context, Props props, RouterState router)
        {
            var routees = Enumerable.Range(0, _poolSize).Select(x => context.Spawn(props));
            router.SetRoutees(new HashSet<PID>(routees));
        }

        public abstract RouterState CreateRouterState();
    }
}