// -----------------------------------------------------------------------
//  <copyright file="RouterProcess.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

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

        public override void SendUserMessage(PID pid, object message)
        {
            var env = MessageEnvelope.Unwrap(message);
            switch (env.message)
            {
                case RouterManagementMessage _:
                    var router = ProcessRegistry.Instance.Get(_router);
                    router.SendUserMessage(pid, message);
                    break;
                default:
                    _state.RouteMessage(message);
                    break;
            }
        }

        public override void SendSystemMessage(PID pid, object message)
        {
            _router.SendSystemMessage(message);
        }
    }
}