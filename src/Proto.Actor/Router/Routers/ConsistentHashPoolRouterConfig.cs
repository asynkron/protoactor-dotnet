// -----------------------------------------------------------------------
//   <copyright file="ConsistentHashPoolRouterConfig.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;

namespace Proto.Router.Routers
{
    internal class ConsistentHashPoolRouterConfig : PoolRouterConfig
    {
        private readonly Func<string, uint> _hash;
        private readonly int _replicaCount;
        private readonly ISenderContext _senderContext;

        public ConsistentHashPoolRouterConfig(ISenderContext senderContext, int poolSize, Props routeeProps,
            Func<string, uint> hash, int replicaCount)
            : base(poolSize, routeeProps)
        {
            _senderContext = senderContext;
            if (replicaCount <= 0)
            {
                throw new ArgumentException("ReplicaCount must be greater than 0");
            }

            _hash = hash;
            _replicaCount = replicaCount;
        }

        public override RouterState CreateRouterState() =>
            new ConsistentHashRouterState(_senderContext, _hash, _replicaCount);
    }
}