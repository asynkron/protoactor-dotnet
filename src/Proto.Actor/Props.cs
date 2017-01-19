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
        public Func<IActor> Producer { get; private set; }

        public Func<IMailbox> MailboxProducer { get; private set; } =
            () => new DefaultMailbox(new UnboundedMailboxQueue(), new UnboundedMailboxQueue());

        public IDispatcher Dispatcher { get; private set; } = Dispatchers.DefaultDispatcher;

        public ISupervisorStrategy Supervisor { get; private set; } = Supervision.DefaultStrategy;

        public Receive[] ReceivePlugins { get; private set; }
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
            return Copy(props => props.Supervisor = supervisor);
        }

        public Props WithReceivers(params Receive[] plugins)
        {
            return Copy(props => props.ReceivePlugins = plugins);
        }

        private Props Copy(Action<Props> mutator)
        {
            var props = new Props
            {
                Dispatcher = Dispatcher,
                MailboxProducer = MailboxProducer,
                Producer = Producer,
                ReceivePlugins = ReceivePlugins,
                Spawner = Spawner,
                Supervisor = Supervisor
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