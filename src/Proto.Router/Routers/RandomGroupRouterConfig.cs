// -----------------------------------------------------------------------
//  <copyright file="RandomGroupRouterConfig.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;

namespace Proto.Router.Routers
{
    internal class RandomGroupRouterConfig : GroupRouterConfig
    {
        public RandomGroupRouterConfig(params PID[] routees)
        {
            Routees = new HashSet<PID>(routees);
        }

        public override RouterState CreateRouterState()
        {
            return new RandomRouterState();
        }
    }
}