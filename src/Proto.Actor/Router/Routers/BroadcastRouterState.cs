// -----------------------------------------------------------------------
// <copyright file="BroadcastRouterState.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
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