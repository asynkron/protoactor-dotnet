// -----------------------------------------------------------------------
//   <copyright file="RouterProcess.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Proto.Mailbox;
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

        protected override Task SendUserMessageAsync(PID pid, object message)
        {
            var (msg,_,_) = MessageEnvelope.Unwrap(message);
            switch (msg)
            {
                case RouterManagementMessage _:
                    return base.SendUserMessageAsync(pid,message);
                default:
                    return _state.RouteMessageAsync(message);
            }
        }
    }
}