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

        public override Task SendUserMessageAsync(PID pid, object message)
        {
            var env = MessageEnvelope.Unwrap(message);
            switch (env.message)
            {
                case RouterManagementMessage _:
                    var router = ProcessRegistry.Instance.Get(_router);
                    return router.SendUserMessageAsync(pid, message);
                default:
                    return _state.RouteMessageAsync(message);
            }
        }

        public override Task SendSystemMessageAsync(PID pid, object message)
        {
            return _router.SendSystemMessageAsync(message);
        }
    }
}