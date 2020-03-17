// -----------------------------------------------------------------------
//   <copyright file="RandomRouterState.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

namespace Proto.Router.Routers
{
    class RandomRouterState : RouterState
    {
        private readonly Random _random;
        private readonly ISenderContext _senderContext;
        private HashSet<PID> _routees;
        private PID[] _values;

        public RandomRouterState(ISenderContext senderContext, int? seed)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
            _senderContext = senderContext;
        }

        public override HashSet<PID> GetRoutees() => _routees;

        public override void SetRoutees(HashSet<PID> routees)
        {
            _routees = routees;
            _values = routees.ToArray();
        }

        public override void RouteMessage(object message)
        {
            var i = _random.Next(_values.Length);
            var pid = _values[i];
            _senderContext.Send(pid, message);
        }
    }
}