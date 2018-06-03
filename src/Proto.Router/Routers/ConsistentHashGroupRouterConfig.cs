// -----------------------------------------------------------------------
//   <copyright file="ConsistentHashGroupRouterConfig.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Proto.Router.Routers
{
    internal class ConsistentHashGroupRouterConfig : GroupRouterConfig
    {
        private readonly Func<string, uint> _hash;
        private readonly int _replicaCount;

        public ConsistentHashGroupRouterConfig(Func<string, uint> hash, int replicaCount, params PID[] routees)
        {
            if (replicaCount <= 0)
            {
                throw new ArgumentException("ReplicaCount must be greater than 0");
            }
            _hash = hash;
            _replicaCount = replicaCount;
            Routees = new HashSet<PID>(routees);
        }

        public override RouterState CreateRouterState() => new ConsistentHashRouterState(_hash, _replicaCount);
    }
}