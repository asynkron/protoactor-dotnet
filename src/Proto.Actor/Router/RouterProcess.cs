// -----------------------------------------------------------------------
// <copyright file="RouterProcess.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Proto.Mailbox;
using Proto.Router.Messages;
using Proto.Router.Routers;

namespace Proto.Router
{
    public class RouterProcess : ActorProcess
    {
        private readonly RouterState _state;

        public RouterProcess(ActorSystem system, RouterState state, IMailbox mailbox) : base(system, mailbox) => _state = state;

        protected internal override void SendUserMessage(PID pid, object message)
        {
            var (msg, _, _) = MessageEnvelope.Unwrap(message);

            switch (msg)
            {
                case RouterManagementMessage _:
                    base.SendUserMessage(pid, message);
                    break;
                default:
                    _state.RouteMessage(message);
                    break;
            }
        }
    }
}