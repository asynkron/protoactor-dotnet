// -----------------------------------------------------------------------
// <copyright file="ConsistentHashGroupRouterConfig.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;

namespace Proto.Router.Routers
{
    record ConsistentHashGroupRouterConfig : GroupRouterConfig
    {
        private readonly Func<string, uint> _hash;
        private readonly Func<object, string>? _messageHasher;
        private readonly int _replicaCount;

        public ConsistentHashGroupRouterConfig(
            ISenderContext senderContext,
            Func<string, uint> hash,
            int replicaCount,
            Func<object, string>? messageHasher,
            params PID[] routees
        )
            : base(senderContext, routees)
        {
            if (replicaCount <= 0) throw new ArgumentException("ReplicaCount must be greater than 0");

            _hash = hash;
            _replicaCount = replicaCount;
            _messageHasher = messageHasher;
        }

        protected override RouterState CreateRouterState() =>
            new ConsistentHashRouterState(SenderContext, _hash, _replicaCount, _messageHasher);
    }
}