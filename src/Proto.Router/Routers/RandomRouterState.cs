// -----------------------------------------------------------------------
//  <copyright file="RandomRouterState.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

namespace Proto.Router.Routers
{
    internal class RandomRouterState : RouterState
    {
        private readonly Random _random = new Random();
        private HashSet<PID> _routees;
        private PID[] _values;

        public override HashSet<PID> GetRoutees()
        {
            return _routees;
        }

        public override void SetRoutees(HashSet<PID> routees)
        {
            _routees = routees;
            _values = routees.ToArray();
        }

        public override void RouteMessage(object message, PID sender)
        {
            var i = _random.Next(_values.Length);
            var pid = _values[i];
            pid.Request(message, sender);
        }
    }
}