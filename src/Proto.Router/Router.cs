// -----------------------------------------------------------------------
//   <copyright file="Router.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using Proto.Router.Routers;

namespace Proto.Router
{
    public class Router
    {
        private readonly ISenderContext _senderContext;
        public Router(ISenderContext senderContext) => _senderContext = senderContext;

        public Props NewBroadcastGroup(params PID[] routees) => new BroadcastGroupRouterConfig(_senderContext, routees).Props();

        public Props NewConsistentHashGroup(params PID[] routees) => new ConsistentHashGroupRouterConfig(_senderContext, MD5Hasher.Hash, 100, routees).Props();

        public Props NewConsistentHashGroup(Func<string, uint> hash, int replicaCount, params PID[] routees)
            => new ConsistentHashGroupRouterConfig(_senderContext, hash, replicaCount, routees).Props();

        public Props NewRandomGroup(params PID[] routees) => new RandomGroupRouterConfig(_senderContext, routees).Props();

        public Props NewRandomGroup(int seed, params PID[] routees) => new RandomGroupRouterConfig(_senderContext, seed, routees).Props();

        public Props NewRoundRobinGroup(params PID[] routees) => new RoundRobinGroupRouterConfig(_senderContext, routees).Props();

        public Props NewBroadcastPool(Props props, int poolSize) => new BroadcastPoolRouterConfig(_senderContext, poolSize, props).Props();

        public Props NewConsistentHashPool(Props props, int poolSize, Func<string, uint> hash = null, int replicaCount = 100)
            => new ConsistentHashPoolRouterConfig(_senderContext, poolSize, props, hash ?? MD5Hasher.Hash, replicaCount).Props();

        public Props NewRandomPool(Props props, int poolSize, int? seed = null) => new RandomPoolRouterConfig(_senderContext, poolSize, props, seed).Props();

        public Props NewRoundRobinPool(Props props, int poolSize) => new RoundRobinPoolRouterConfig(_senderContext, poolSize, props).Props();
    }
}