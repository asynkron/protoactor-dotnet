// -----------------------------------------------------------------------
//   <copyright file="RouterState.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

namespace Proto.Router.Routers
{
    public abstract class RouterState
    {
        private HashSet<PID>? _routees;
        private List<PID>? _values;

        public virtual HashSet<PID> GetRoutees()
        {
            if (_routees == null)
                throw new InvalidOperationException("Routees not set");

            return _routees;
        }

        protected List<PID> GetValues()
        {
            if (_values == null)
                throw new InvalidOperationException("Routees not set");

            return _values;
        }

        public virtual void SetRoutees(HashSet<PID> routees)
        {
            _routees = routees;
            _values = routees.ToList();
        }

        public abstract void RouteMessage(object message);
    }
}
