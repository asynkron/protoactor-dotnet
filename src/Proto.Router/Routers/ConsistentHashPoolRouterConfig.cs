// -----------------------------------------------------------------------
//   <copyright file="ConsistentHashPoolRouterConfig.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;

namespace Proto.Router.Routers
{
    class ConsistentHashPoolRouterConfig : PoolRouterConfig
    {
        private readonly Func<string, uint> _hash;
        private readonly int _replicaCount;
        private readonly ActorSystem _system;

        public ConsistentHashPoolRouterConfig(ActorSystem system, int poolSize, Props routeeProps, Func<string, uint> hash, int replicaCount)
            : base(poolSize, routeeProps)
        {
            _system = system;
            if (replicaCount <= 0)
            {
                throw new ArgumentException("ReplicaCount must be greater than 0");
            }
            _hash = hash;
            _replicaCount = replicaCount;
        }

        public override RouterState CreateRouterState() => new ConsistentHashRouterState(_system, _hash, _replicaCount);
    }
}