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
        private readonly ActorSystem _system;
        public Router(ActorSystem system) => _system = system;

        public Props NewBroadcastGroup(params PID[] routees) => new BroadcastGroupRouterConfig(_system, routees).Props();

        public Props NewConsistentHashGroup(params PID[] routees) => new ConsistentHashGroupRouterConfig(_system, MD5Hasher.Hash, 100, routees).Props();

        public Props NewConsistentHashGroup(Func<string, uint> hash, int replicaCount, params PID[] routees)
            => new ConsistentHashGroupRouterConfig(_system, hash, replicaCount, routees).Props();

        public Props NewRandomGroup(params PID[] routees) => new RandomGroupRouterConfig(_system, routees).Props();

        public Props NewRandomGroup(int seed, params PID[] routees) => new RandomGroupRouterConfig(_system, seed, routees).Props();

        public Props NewRoundRobinGroup(params PID[] routees) => new RoundRobinGroupRouterConfig(_system, routees).Props();

        public Props NewBroadcastPool(Props props, int poolSize) => new BroadcastPoolRouterConfig(_system, poolSize, props).Props();

        public Props NewConsistentHashPool(Props props, int poolSize, Func<string, uint> hash = null, int replicaCount = 100)
            => new ConsistentHashPoolRouterConfig(_system, poolSize, props, hash ?? MD5Hasher.Hash, replicaCount).Props();

        public Props NewRandomPool(Props props, int poolSize, int? seed = null) => new RandomPoolRouterConfig(_system, poolSize, props, seed).Props();

        public Props NewRoundRobinPool(Props props, int poolSize) => new RoundRobinPoolRouterConfig(_system, poolSize, props).Props();
    }
}