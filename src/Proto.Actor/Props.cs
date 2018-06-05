// -----------------------------------------------------------------------
//   <copyright file="Props.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using Proto.Mailbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Proto
{
    public sealed class Props
    {
        private Spawner _spawner;
        public Func<IActor> Producer { get; private set; }
        public Func<IMailbox> MailboxProducer { get; private set; } = ProduceDefaultMailbox;
        public ISupervisorStrategy GuardianStrategy { get; private set; }
        public ISupervisorStrategy SupervisorStrategy { get; private set; } = Supervision.DefaultStrategy;
        public IDispatcher Dispatcher { get; private set; } = Dispatchers.DefaultDispatcher;
        public IList<Func<Receive, Receive>> ReceiveMiddleware { get; private set; } = new List<Func<Receive, Receive>>();
        public IList<Func<Sender, Sender>> SenderMiddleware { get; private set; } = new List<Func<Sender, Sender>>();
        public Receive ReceiveMiddlewareChain { get; private set; }
        public Sender SenderMiddlewareChain { get; private set; }

        public Spawner Spawner
        {
            get => _spawner ?? DefaultSpawner;
            private set => _spawner = value;
        }

        private static IMailbox ProduceDefaultMailbox() => UnboundedMailbox.Create();

        private static PID DefaultSpawner (string name,Props props,PID parent)
        {
            var ctx = new ActorContext(props.Producer, props.SupervisorStrategy, props.ReceiveMiddlewareChain, props.SenderMiddlewareChain, parent);
            var mailbox = props.MailboxProducer();
            var dispatcher = props.Dispatcher;
            var process = new LocalProcess(mailbox);
            var (pid, absent) = ProcessRegistry.Instance.TryAdd(name, process);
            if (!absent)
            {
                throw new ProcessNameExistException(name, pid);
            }
            ctx.Self = pid;
            mailbox.RegisterHandlers(ctx, dispatcher);
            mailbox.PostSystemMessage(Started.Instance);
            mailbox.Start();

            return pid;
        }

        public Props WithProducer(Func<IActor> producer) => Copy(props => props.Producer = producer);

        public Props WithDispatcher(IDispatcher dispatcher) => Copy(props => props.Dispatcher = dispatcher);

        public Props WithMailbox(Func<IMailbox> mailboxProducer) => Copy(props => props.MailboxProducer = mailboxProducer);

        public Props WithGuardianSupervisorStrategy(ISupervisorStrategy guardianStrategy) => Copy(props => props.GuardianStrategy = guardianStrategy);

        public Props WithChildSupervisorStrategy(ISupervisorStrategy supervisorStrategy) => Copy(props => props.SupervisorStrategy = supervisorStrategy);

        public Props WithReceiveMiddleware(params Func<Receive, Receive>[] middleware) => Copy(props =>
        {
            props.ReceiveMiddleware = ReceiveMiddleware.Concat(middleware).ToList();
            props.ReceiveMiddlewareChain = props.ReceiveMiddleware.Reverse()
                                                .Aggregate((Receive) ActorContext.DefaultReceive, (inner, outer) => outer(inner));
        });

        public Props WithSenderMiddleware(params Func<Sender, Sender>[] middleware) => Copy(props =>
        {
            props.SenderMiddleware = SenderMiddleware.Concat(middleware).ToList();
            props.SenderMiddlewareChain = props.SenderMiddleware.Reverse()
                                               .Aggregate((Sender) ActorContext.DefaultSender, (inner, outer) => outer(inner));
        });

        public Props WithSpawner(Spawner spawner) => Copy(props => props.Spawner = spawner);

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

        internal PID Spawn(string name, PID parent) => Spawner(name, this, parent);
    }

    public delegate PID Spawner(string id, Props props, PID parent);
}