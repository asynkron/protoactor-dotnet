// -----------------------------------------------------------------------
//   <copyright file="Props.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Proto.Context;
using Proto.Mailbox;

namespace Proto
{
    [PublicAPI]
    public sealed class Props
    {
        private Spawner? _spawner;
        public Producer Producer { get; private set; } = () => null!;
        public MailboxProducer MailboxProducer { get; private set; } = ProduceDefaultMailbox;
        public ISupervisorStrategy? GuardianStrategy { get; private set; }
        public ISupervisorStrategy SupervisorStrategy { get; private set; } = Supervision.DefaultStrategy;
        public IDispatcher Dispatcher { get; private set; } = Dispatchers.DefaultDispatcher;

        public IList<Func<Receiver, Receiver>> ReceiverMiddleware { get; private set; } =
            new List<Func<Receiver, Receiver>>();

        public IList<Func<Sender, Sender>> SenderMiddleware { get; private set; } = new List<Func<Sender, Sender>>();
        public Receiver? ReceiverMiddlewareChain { get; private set; }
        public Sender? SenderMiddlewareChain { get; private set; }

        public IList<Func<IContext, IContext>> ContextDecorator { get; private set; } =
            new List<Func<IContext, IContext>>();

        public Func<IContext, IContext>? ContextDecoratorChain { get; private set; }

        public Spawner Spawner
        {
            get => _spawner ?? DefaultSpawner;
            private set => _spawner = value;
        }

        private static IContext DefaultContextDecorator(IContext context) => context;

        private static IMailbox ProduceDefaultMailbox() => UnboundedMailbox.Create();

        public static PID DefaultSpawner(ActorSystem system, string name, Props props, PID? parent)
        {
            var mailbox = props.MailboxProducer();
            var dispatcher = props.Dispatcher;
            var process = new ActorProcess(system, mailbox);
            var (self, absent) = system.ProcessRegistry.TryAdd(name, process);

            if (!absent)
            {
                throw new ProcessNameExistException(name, self);
            }

            var ctx = ActorContext.Setup(system, props, parent, self);
            mailbox.RegisterHandlers(ctx, dispatcher);
            mailbox.PostSystemMessage(Started.Instance);
            mailbox.Start();

            return self;
        }

        public Props WithProducer(Producer producer) => Copy(props => props.Producer = producer);

        public Props WithDispatcher(IDispatcher dispatcher) => Copy(props => props.Dispatcher = dispatcher);

        public Props WithMailbox(MailboxProducer mailboxProducer) =>
            Copy(props => props.MailboxProducer = mailboxProducer);

        public Props WithContextDecorator(params Func<IContext, IContext>[] contextDecorator)
            => Copy(
                props =>
                {
                    props.ContextDecorator = ContextDecorator.Concat(contextDecorator).ToList();

                    props.ContextDecoratorChain = props.ContextDecorator.Reverse()
                        .Aggregate(
                            (Func<IContext, IContext>) DefaultContextDecorator,
                            (inner, outer) => ctx => outer(inner(ctx))
                        );
                }
            );

        public Props WithGuardianSupervisorStrategy(ISupervisorStrategy guardianStrategy) =>
            Copy(props => props.GuardianStrategy = guardianStrategy);

        public Props WithChildSupervisorStrategy(ISupervisorStrategy supervisorStrategy)
            => Copy(props => props.SupervisorStrategy = supervisorStrategy);

        public Props WithReceiverMiddleware(params Func<Receiver, Receiver>[] middleware)
            => Copy(
                props =>
                {
                    props.ReceiverMiddleware = ReceiverMiddleware.Concat(middleware).ToList();

                    props.ReceiverMiddlewareChain = props.ReceiverMiddleware.Reverse()
                        .Aggregate((Receiver) Middleware.Receive, (inner, outer) => outer(inner));
                }
            );

        public Props WithSenderMiddleware(params Func<Sender, Sender>[] middleware)
            => Copy(
                props =>
                {
                    props.SenderMiddleware = SenderMiddleware.Concat(middleware).ToList();

                    props.SenderMiddlewareChain = props.SenderMiddleware.Reverse()
                        .Aggregate((Sender) Middleware.Sender, (inner, outer) => outer(inner));
                }
            );

        public Props WithSpawner(Spawner spawner) => Copy(props => props.Spawner = spawner);

        private Props Copy(Action<Props> mutator)
        {
            var props = new Props
            {
                Dispatcher = Dispatcher,
                MailboxProducer = MailboxProducer,
                Producer = Producer,
                ReceiverMiddleware = ReceiverMiddleware,
                ReceiverMiddlewareChain = ReceiverMiddlewareChain,
                SenderMiddleware = SenderMiddleware,
                SenderMiddlewareChain = SenderMiddlewareChain,
                Spawner = Spawner,
                SupervisorStrategy = SupervisorStrategy,
                GuardianStrategy = GuardianStrategy,
                ContextDecorator = ContextDecorator,
                ContextDecoratorChain = ContextDecoratorChain
            };
            mutator(props);
            return props;
        }

        internal PID Spawn(ActorSystem system, string name, PID? parent) => Spawner(system, name, this, parent);
        public static Props FromProducer(Producer producer) => new Props().WithProducer(producer);
        public static Props FromFunc(Receive receive) => FromProducer(() => new FunctionActor(receive));
    }

    public delegate PID Spawner(ActorSystem system, string id, Props props, PID? parent);

    public delegate IActor Producer();

    public delegate IMailbox MailboxProducer();
}