// -----------------------------------------------------------------------
//   <copyright file="Router.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using Proto.Router.Routers;

namespace Proto.Router
{
    public static class Router
    {
        public static Props NewBroadcastGroup(params PID[] routees) => new BroadcastGroupRouterConfig(routees).Props();

        public static Props NewConsistentHashGroup(params PID[] routees) => new ConsistentHashGroupRouterConfig(MD5Hasher.Hash, 100, routees).Props();

        public static Props NewConsistentHashGroup(Func<string, uint> hash, int replicaCount, params PID[] routees) => new ConsistentHashGroupRouterConfig(hash, replicaCount, routees).Props();

        public static Props NewRandomGroup(params PID[] routees) => new RandomGroupRouterConfig(routees).Props();

        public static Props NewRandomGroup(int seed, params PID[] routees) => new RandomGroupRouterConfig(seed, routees).Props();

        public static Props NewRoundRobinGroup(params PID[] routees) => new RoundRobinGroupRouterConfig(routees).Props();

        public static Props NewBroadcastPool(Props props, int poolSize) => new BroadcastPoolRouterConfig(poolSize, props).Props();

        public static Props NewConsistentHashPool(Props props, int poolSize, Func<string, uint> hash = null, int replicaCount = 100) => new ConsistentHashPoolRouterConfig(poolSize, props, hash ?? MD5Hasher.Hash, replicaCount).Props();

        public static Props NewRandomPool(Props props, int poolSize, int? seed = null) => new RandomPoolRouterConfig(poolSize, props, seed).Props();

        public static Props NewRoundRobinPool(Props props, int poolSize) => new RoundRobinPoolRouterConfig(poolSize, props).Props();
    }
}