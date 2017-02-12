// -----------------------------------------------------------------------
//  <copyright file="ConsistentHashRouterState.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;

namespace Proto.Router.Routers
{
    internal class ConsistentHashRouterState : RouterState
    {
        private HashRing _hashRing;
        private Dictionary<string, PID> _routeeMap;


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
                var nodeName = pid.Address + "@" + pid.Id;
                nodes.Add(nodeName);
                _routeeMap[nodeName] = pid;
            }
            _hashRing = new HashRing(nodes);
        }

        public override void RouteMessage(object message, PID sender)
        {
            if (message is IHashable hashable)
            {
                var key = hashable.HashBy();
                var node = _hashRing.GetNode(key);
                var routee = _routeeMap[node];
                routee.Request(message, sender);
            }
        }
    }
}