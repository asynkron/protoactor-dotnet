// -----------------------------------------------------------------------
//   <copyright file="ConsistentHashRouterState.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Proto.Router.Routers
{
    internal class ConsistentHashRouterState : RouterState
    {
        private readonly Func<string, uint> _hash;
        private readonly int _replicaCount;
        private HashRing _hashRing;
        private Dictionary<string, PID> _routeeMap;

        public ConsistentHashRouterState(Func<string, uint> hash, int replicaCount)
        {
            _hash = hash;
            _replicaCount = replicaCount;
        }

        public override HashSet<PID> GetRoutees()
        {
            return new HashSet<PID>(_routeeMap.Values);
        }

        public override void SetRoutees(HashSet<PID> routees)
        {
            _routeeMap = new Dictionary<string, PID>();
            var nodes = new List<string>();
            foreach (var pid in routees)
            {
                var nodeName = pid.ToShortString();
                nodes.Add(nodeName);
                _routeeMap[nodeName] = pid;
            }
            _hashRing = new HashRing(nodes, _hash, _replicaCount);
        }

        public override void RouteMessage(object message)
        {
            var env = MessageEnvelope.Unwrap(message);
            if (env.message is IHashable hashable)
            {
                var key = hashable.HashBy();
                var node = _hashRing.GetNode(key);
                var routee = _routeeMap[node];
                
                //by design, just forward message
                RootContext.Empty.Send(routee, message);
            }
            else
            {
                throw new NotSupportedException($"Message of type '{message.GetType().Name}' does not implement IHashable");
            }

        }
    }
}