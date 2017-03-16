// -----------------------------------------------------------------------
//  <copyright file="Props.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
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
            () => UnboundedMailbox.Create();

        public ISupervisorStrategy SupervisorStrategy { get; private set; } = Supervision.DefaultStrategy;

        public IDispatcher Dispatcher { get; private set; } = Dispatchers.DefaultDispatcher;

        public IList<Func<Receive, Receive>> ReceiveMiddleware { get; private set; } = new List<Func<Receive, Receive>>();
        public IList<Func<Sender, Sender>> SenderMiddleware { get; private set; } = new List<Func<Sender, Sender>>();

        public Receive ReceiveMiddlewareChain { get; set; }
        public Sender SenderMiddlewareChain { get; set; }

        private Spawner _spawner = null;
        public Spawner Spawner {
            get => _spawner ?? DefaultSpawner;
            private set => _spawner = value;
        }

        public static Spawner DefaultSpawner = (name, props, parent) =>
        {
            var ctx = new Context(props.Producer, props.SupervisorStrategy, props.ReceiveMiddlewareChain, props.SenderMiddlewareChain, parent);
            var mailbox = props.MailboxProducer();
            var dispatcher = props.Dispatcher;
            var reff = new LocalProcess(mailbox);
            var (pid, absent) = ProcessRegistry.Instance.TryAdd(name, reff);
            if (!absent)
            {
                throw new ProcessNameExistException(name);
            }
            ctx.Self = pid;
            mailbox.RegisterHandlers(ctx, dispatcher);
            mailbox.PostSystemMessage(Started.Instance);
            mailbox.Start();

            return pid;
        };

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

        public Props WithReceiveMiddleware(params Func<Receive, Receive>[] middleware)
        {
            return Copy(props =>
            {
                props.ReceiveMiddleware = ReceiveMiddleware.Concat(middleware).ToList();
                props.ReceiveMiddlewareChain = props.ReceiveMiddleware.Reverse()
                    .Aggregate((Receive) Context.DefaultReceive, (inner, outer) => outer(inner));
            });
        }

        public Props WithSenderMiddleware(params Func<Sender, Sender>[] middleware)
        {
            return Copy(props =>
            {
                props.SenderMiddleware = SenderMiddleware.Concat(middleware).ToList();
                props.SenderMiddlewareChain = props.SenderMiddleware.Reverse()
                    .Aggregate((Sender) Context.DefaultSender, (inner, outer) => outer(inner));
            });
        }

        public Props WithSpawner(Spawner spawner)
        {
            return Copy(props => props.Spawner = spawner);
        }

        private Props Copy(Action<Props> mutator)
        {
            var props = new Props
            {
                Dispatcher = Dispatcher,
                MailboxProducer = MailboxProducer,
                Producer = Producer,
                ReceiveMiddleware = ReceiveMiddleware,
                ReceiveMiddlewareChain = ReceiveMiddlewareChain,
                SenderMiddleware = SenderMiddleware,
                SenderMiddlewareChain = SenderMiddlewareChain,
                Spawner = Spawner,
                SupervisorStrategy = SupervisorStrategy
            };
            mutator(props);
            return props;
        }

        public PID Spawn(string name, PID parent)
        {
            return Spawner(name, this, parent);
        }
    }

    public delegate PID Spawner(string id, Props props, PID parent);
}