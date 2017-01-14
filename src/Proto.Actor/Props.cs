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
        private IDispatcher _dispatcher;
        private Func<IMailbox> _mailboxProducer;
        private ISupervisorStrategy _supervisor;
        private IRouterConfig _routerConfig;

        public Func<IActor> Producer { get; private set; }

        public Func<IMailbox> MailboxProducer => _mailboxProducer ?? (() => new DefaultMailbox(new UnboundedMailboxQueue(), new UnboundedMailboxQueue()));
        //public Func<IMailbox> MailboxProducer => _mailboxProducer ?? (() => new DefaultMailbox(new BoundedMailboxQueue(4), new BoundedMailboxQueue(4)));

        public IDispatcher Dispatcher => _dispatcher ?? new ThreadPoolDispatcher();

        public ISupervisorStrategy Supervisor => _supervisor ?? Supervision.DefaultStrategy;

        public IRouterConfig RouterConfig => _routerConfig;

        public Props WithDispatcher(IDispatcher dispatcher)
        {
            return Copy(dispatcher: dispatcher);
        }

        public Props Copy(Func<IActor> producer = null, IDispatcher dispatcher = null,
            Func<IMailbox> mailboxProducer = null, ISupervisorStrategy supervisor = null,
            IRouterConfig routerConfig = null)
        {
            return new Props
            {
                Producer = producer ?? Producer,
                _dispatcher = dispatcher ?? Dispatcher,
                _mailboxProducer = mailboxProducer ?? _mailboxProducer,
                _routerConfig = routerConfig,
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

        public Props WithPoolRouter(IPoolRouterConfig routerConfig)
        {
            return Copy(routerConfig: routerConfig);
        }
    }
}