// -----------------------------------------------------------------------
//   <copyright file="ConsistentHashRouterState.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Proto.Router.Routers
{
    class ConsistentHashRouterState : RouterState
    {
        private readonly Func<string, uint> _hash;
        private readonly int _replicaCount;
        private HashRing? _hashRing;
        private readonly Dictionary<string, PID> _routeeMap = new Dictionary<string, PID>();
        private readonly ISenderContext _senderContext;

        public ConsistentHashRouterState(ISenderContext senderContext, Func<string, uint> hash, int replicaCount)
        {
            _senderContext = senderContext;
            _hash = hash;
            _replicaCount = replicaCount;
        }

        public override HashSet<PID> GetRoutees() => new HashSet<PID>(_routeeMap.Values);

        public override void SetRoutees(HashSet<PID> routees)
        {
            _routeeMap.Clear();
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
            if (_hashRing == null)
                throw new InvalidOperationException("Routees not set");

            var env = MessageEnvelope.Unwrap(message);

            if (env.message is IHashable hashable)
            {
                var key = hashable.HashBy();
                var node = _hashRing.GetNode(key);
                var routee = _routeeMap[node];

                //by design, just forward message
                _senderContext.Send(routee, message);
            }
            else
            {
                throw new NotSupportedException($"Message of type '{message.GetType().Name}' does not implement IHashable");
            }
        }
    }
}