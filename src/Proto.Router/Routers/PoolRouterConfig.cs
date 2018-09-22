// -----------------------------------------------------------------------
//   <copyright file="PoolRouterConfig.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

namespace Proto.Router.Routers
{
    internal abstract class PoolRouterConfig : RouterConfig
    {
        private readonly int _poolSize;
        private readonly Props _routeeProps;

        protected PoolRouterConfig(int poolSize, Props routeeProps)
        {
            _poolSize = poolSize;
            _routeeProps = routeeProps;
        }

        public override void OnStarted(IContext context, RouterState router)
        {
            var routees = Enumerable.Range(0, _poolSize).Select(x => context.Spawn(_routeeProps));
            router.SetRoutees(new HashSet<PID>(routees));
        }
    }
}