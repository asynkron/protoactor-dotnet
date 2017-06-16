// -----------------------------------------------------------------------
//  <copyright file="RouterState.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Proto.Router.Routers
{
    public abstract class RouterState
    {
        public abstract HashSet<PID> GetRoutees();
        public abstract void SetRoutees(HashSet<PID> routees);
        public abstract Task RouteMessageAsync(object message);
    }
}