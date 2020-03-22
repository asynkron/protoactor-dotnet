// -----------------------------------------------------------------------
//   <copyright file="BroadcastRouterState.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Proto.Router.Routers
{
    class BroadcastRouterState : RouterState
    {
        private readonly ISenderContext _senderContext;

        internal BroadcastRouterState(ISenderContext senderContext) => _senderContext = senderContext;

        public override void RouteMessage(object message)
        {
            foreach (var pid in GetRoutees())
            {
                _senderContext.Send(pid, message);
            }
        }
    }
}
