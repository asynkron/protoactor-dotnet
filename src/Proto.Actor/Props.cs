// -----------------------------------------------------------------------
//  <copyright file="Props.cs" company="Asynkron HB">
//      Copyright (C) 2015-2016 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Proto.Mailbox;

namespace Proto
{
    public sealed class Props
    {
        public Func<IActor> Producer { get; private set; }

        public Func<IMailbox> MailboxProducer { get; private set; } =
            () => new DefaultMailbox(new UnboundedMailboxQueue(), new UnboundedMailboxQueue());

        public ISupervisorStrategy SupervisorStrategy { get; private set; } = Supervision.DefaultStrategy;

        public IDispatcher Dispatcher { get; private set; } = Dispatchers.DefaultDispatcher;

        public IList<Func<Receive, Receive>> Middleware { get; private set; } = new List<Func<Receive, Receive>>();

        public Receive MiddlewareChain { get; set; }

        public Spawner Spawner { get; private set; }


        public Props WithProducer(Func<IActor> producer)
        {
            return Copy(props => props.Producer = producer);
        }

        public Props WithDispatcher(IDispatcher dispatcher)
        {
            return Copy(props => props.Dispatcher = dispatcher);
        }

        public Props WithMailbox(Func<IMailbox> mailboxProducer)
        {
            return Copy(props => props.MailboxProducer = mailboxProducer);
        }

        public Props WithSupervisor(ISupervisorStrategy supervisor)
        {
            return Copy(props => props.SupervisorStrategy = supervisor);
        }

        public Props WithMiddleware(params Func<Receive,Receive>[] middleware)
        {
            Middleware = Middleware.Concat(middleware).ToList();
            MiddlewareChain = Middleware.Reverse().Aggregate((Receive) Context.DefaultReceive, (inner, outer) => outer(inner));
            return this;
        }

        private Props Copy(Action<Props> mutator)
        {
            var props = new Props
            {
                Dispatcher = Dispatcher,
                MailboxProducer = MailboxProducer,
                Producer = Producer,
                Middleware = Middleware,
                Spawner = Spawner,
                SupervisorStrategy = SupervisorStrategy
            };
            mutator(props);
            return props;
        }

        public Props WithSpawner(Spawner spawner)
        {
            return Copy(props => props.Spawner = spawner);
        }
    }

    public delegate PID Spawner(string id, Props props, PID parent);
}