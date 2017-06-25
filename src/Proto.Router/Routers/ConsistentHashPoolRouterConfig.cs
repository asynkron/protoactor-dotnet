// -----------------------------------------------------------------------
//   <copyright file="ConsistentHashPoolRouterConfig.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;

namespace Proto.Router.Routers
{
    internal class ConsistentHashPoolRouterConfig : PoolRouterConfig
    {
        private readonly Func<string, uint> _hash;
        private readonly int _replicaCount;

        public ConsistentHashPoolRouterConfig(int poolSize, Func<string, uint> hash, int replicaCount)
            : base(poolSize)
        {
            if (replicaCount <= 0)
            {
                throw new ArgumentException("ReplicaCount must be greater than 0");
            }
            _hash = hash;
            _replicaCount = replicaCount;
        }

        public override RouterState CreateRouterState()
        {
            return new ConsistentHashRouterState(_hash, _replicaCount);
        }
    }
}