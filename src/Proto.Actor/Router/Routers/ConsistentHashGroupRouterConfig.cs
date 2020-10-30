// -----------------------------------------------------------------------
//   <copyright file="ConsistentHashGroupRouterConfig.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;

namespace Proto.Router.Routers
{
    internal record ConsistentHashGroupRouterConfig : GroupRouterConfig
    {
        private readonly Func<string, uint> _hash;
        private readonly int _replicaCount;

        public ConsistentHashGroupRouterConfig(ISenderContext senderContext, Func<string, uint> hash, int replicaCount,
            params PID[] routees)
            : base(senderContext, routees)
        {
            if (replicaCount <= 0)
            {
                throw new ArgumentException("ReplicaCount must be greater than 0");
            }

            _hash = hash;
            _replicaCount = replicaCount;
        }

        protected override RouterState CreateRouterState() =>
            new ConsistentHashRouterState(SenderContext, _hash, _replicaCount);
    }
}