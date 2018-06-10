// -----------------------------------------------------------------------
//   <copyright file="ConsistentHashPoolRouterConfig.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;

namespace Proto.Router.Routers
{
    internal class ConsistentHashPoolRouterConfig : PoolRouterConfig
    {
        private readonly Func<string, uint> _hash;
        private readonly int _replicaCount;

        public ConsistentHashPoolRouterConfig(int poolSize, Props routeeProps, Func<string, uint> hash, int replicaCount)
            : base(poolSize,routeeProps)
        {
            if (replicaCount <= 0)
            {
                throw new ArgumentException("ReplicaCount must be greater than 0");
            }
            _hash = hash;
            _replicaCount = replicaCount;
        }

        public override RouterState CreateRouterState() => new ConsistentHashRouterState(_hash, _replicaCount);
    }
}