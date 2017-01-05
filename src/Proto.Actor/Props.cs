// -----------------------------------------------------------------------
//  <copyright file="Props.cs" company="Asynkron HB">
//      Copyright (C) 2015-2016 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;

namespace Proto
{
    public sealed class Props
    {
        private Func<IActor> _actorProducer;

        private IDispatcher _dispatcher;
        private Func<IMailbox> _mailboxProducer;
        private ISupervisorStrategy _supervisor;

        public Func<IActor> Producer => _actorProducer;
        public Func<IMailbox> MailboxProducer => _mailboxProducer ?? (() => new DefaultMailbox());

        public IDispatcher Dispatcher => _dispatcher ?? new ThreadPoolDispatcher();

        public Props WithDispatcher(IDispatcher dispatcher)
        {
            return Copy(dispatcher: dispatcher);
        }

        public ISupervisorStrategy Supervisor => _supervisor ?? Supervision.DefaultStrategy;

        public Props Copy(Func<IActor> producer = null, IDispatcher dispatcher = null,
            Func<IMailbox> mailboxProducer = null, ISupervisorStrategy supervisor = null)
        {
            return new Props()
            {
                _actorProducer = producer ?? _actorProducer,
                _dispatcher = dispatcher ?? Dispatcher,
                _mailboxProducer = mailboxProducer ?? _mailboxProducer,
                _supervisor = supervisor,
            };
        }

        public Props WithMailbox(Func<IMailbox> mailboxProducer)
        {
            return Copy(mailboxProducer: mailboxProducer);
        }

        public Props WithSupervisor(ISupervisorStrategy supervisor)
        {
            return Copy(supervisor: supervisor);
        }
    }
}