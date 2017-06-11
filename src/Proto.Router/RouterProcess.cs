// -----------------------------------------------------------------------
//  <copyright file="RouterProcess.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Proto.Router.Messages;
using Proto.Router.Routers;

namespace Proto.Router
{
    public class RouterProcess : Process
    {
        private readonly PID _router;
        private readonly RouterState _state;

        public RouterProcess(PID router, RouterState state)
        {
            _router = router;
            _state = state;
        }

        protected override Task SendUserMessage(PID pid, object message)
        {
            var env = MessageEnvelope.Unwrap(message);
            switch (env.message)
            {
                case RouterManagementMessage _:
                    return _router.Tell(message);
                default:
                    _state.RouteMessage(message);
                    return Task.FromResult(0);
            }
        }

        protected override Task SendSystemMessage(PID pid, object message)
        {
            return _router.SendSystemMessage(message);
        }
    }
}