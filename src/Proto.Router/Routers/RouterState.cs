// -----------------------------------------------------------------------
//   <copyright file="RouterState.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;

namespace Proto.Router.Routers
{
    public abstract class RouterState
    {
        public abstract HashSet<PID> GetRoutees();
        public abstract void SetRoutees(HashSet<PID> routees);
        public abstract void RouteMessage(object message);
    }
}