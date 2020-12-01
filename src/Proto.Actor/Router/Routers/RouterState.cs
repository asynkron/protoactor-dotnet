// -----------------------------------------------------------------------
// <copyright file="RouterState.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Proto.Router.Routers
{
    public abstract class RouterState
    {
        protected HashSet<PID> Routees = new();
        protected ImmutableList<PID> Values = ImmutableList<PID>.Empty;

        public virtual HashSet<PID> GetRoutees() => Routees;

        protected ImmutableList<PID> GetValues() => Values;

        public virtual void SetRoutees(PID[] routees)
        {
            Routees = routees.ToHashSet();
            Values = routees.ToImmutableList();
        }

        public abstract void RouteMessage(object message);
    }
}