// -----------------------------------------------------------------------
//  <copyright file="RandomRouterState.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Proto.Router.Routers
{
    internal class RandomRouterState : RouterState
    {
        private readonly Random _random;
        private HashSet<PID> _routees;
        private PID[] _values;

        public RandomRouterState(int? seed)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public override HashSet<PID> GetRoutees()
        {
            return _routees;
        }

        public override void SetRoutees(HashSet<PID> routees)
        {
            _routees = routees;
            _values = routees.ToArray();
        }

        public override async Task RouteMessageAsync(object message)
        {
            var i = _random.Next(_values.Length);
            var pid = _values[i];
            await pid.SendAsync(message);
        }
    }
}