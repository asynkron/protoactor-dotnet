// -----------------------------------------------------------------------
//   <copyright file="RouterProcess.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using Proto.Router.Messages;
using Proto.Router.Routers;

namespace Proto.Router
{
    public class RouterProcess : LocalProcess
    {
        private readonly RouterState _state;

        public RouterProcess(RouterState state, IMailbox mailbox) : base(mailbox)
        {
            _state = state;
        }

        protected override void SendUserMessage(PID pid, object message)
        {
            var (msg,_,_) = MessageEnvelope.Unwrap(message);
            switch (msg)
            {
                case RouterManagementMessage _:
                    base.SendUserMessage(pid,message);
                    break;
                default:
                    _state.RouteMessage(message);
                    break;
            }
        }
    }
}