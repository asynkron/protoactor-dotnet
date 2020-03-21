// -----------------------------------------------------------------------
//   <copyright file="Router.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using Proto.Router.Routers;

namespace Proto.Router
{
    public static class RouterExtensions
    {
        public static Props NewBroadcastGroup(this ISenderContext senderContext, params PID[] routees)
            => new BroadcastGroupRouterConfig(senderContext, routees).Props();

        public static Props NewConsistentHashGroup(this ISenderContext senderContext, params PID[] routees)
            => new ConsistentHashGroupRouterConfig(senderContext, MD5Hasher.Hash, 100, routees).Props();

        public static Props NewConsistentHashGroup(this ISenderContext senderContext, Func<string, uint> hash, int replicaCount, params PID[] routees)
            => new ConsistentHashGroupRouterConfig(senderContext, hash, replicaCount, routees).Props();

        public static Props NewRandomGroup(this ISenderContext senderContext, params PID[] routees)
            => new RandomGroupRouterConfig(senderContext, routees).Props();

        public static Props NewRandomGroup(this ISenderContext senderContext, int seed, params PID[] routees)
            => new RandomGroupRouterConfig(senderContext, seed, routees).Props();

        public static Props NewRoundRobinGroup(this ISenderContext senderContext, params PID[] routees)
            => new RoundRobinGroupRouterConfig(senderContext, routees).Props();

        public static Props NewBroadcastPool(this ISenderContext senderContext, Props props, int poolSize)
            => new BroadcastPoolRouterConfig(senderContext, poolSize, props).Props();

        public static Props NewConsistentHashPool(this ISenderContext senderContext, Props props, int poolSize, Func<string, uint>? hash = null, int replicaCount = 100)
            => new ConsistentHashPoolRouterConfig(senderContext, poolSize, props, hash ?? MD5Hasher.Hash, replicaCount).Props();

        public static Props NewRandomPool(this ISenderContext senderContext, Props props, int poolSize, int? seed = null)
            => new RandomPoolRouterConfig(senderContext, poolSize, props, seed).Props();

        public static Props NewRoundRobinPool(this ISenderContext senderContext, Props props, int poolSize)
            => new RoundRobinPoolRouterConfig(senderContext, poolSize, props).Props();
    }
}