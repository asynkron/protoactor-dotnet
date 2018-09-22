// -----------------------------------------------------------------------
//   <copyright file="RouterProcess.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using Proto.Router.Messages;
using Proto.Router.Routers;
using Proto.Mailbox;

namespace Proto.Router
{
    public class RouterProcess : ActorProcess
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