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
            () => new DefaultMailbox(new UnboundedMailboxQueue(), new UnboundedMailboxQueue());

        public ISupervisorStrategy SupervisorStrategy { get; private set; } = Supervision.DefaultStrategy;

        public IDispatcher Dispatcher { get; private set; } = Dispatchers.DefaultDispatcher;

        public IList<Func<Receive, Receive>> Middleware { get; private set; } = new List<Func<Receive, Receive>>();

        public Receive MiddlewareChain { get; set; }

        private Spawner _spawner = null;
        public Spawner Spawner {
            get => _spawner ?? DefaultSpawner;
            private set => _spawner = value;
        }

        public static Spawner DefaultSpawner = (name, props, parent) =>
        {
            var ctx = new Context(props.Producer, props.SupervisorStrategy, props.MiddlewareChain, parent);
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
            // ctx.InvokeUserMessageAsync(Started.Instance);
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

        public Props WithMiddleware(params Func<Receive, Receive>[] middleware)
        {
            return Copy(props =>
            {
                props.Middleware = Middleware.Concat(middleware).ToList();
                props.MiddlewareChain = props.Middleware.Reverse()
                    .Aggregate((Receive) Context.DefaultReceive, (inner, outer) => outer(inner));
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
                Middleware = Middleware,
                MiddlewareChain = MiddlewareChain,
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