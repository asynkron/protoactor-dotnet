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
    public sealed record Props
    {
        private Spawner? _spawner;
        public Producer Producer { get; init; } = () => null!;
        public MailboxProducer MailboxProducer { get; init; } = ProduceDefaultMailbox;
        public ISupervisorStrategy? GuardianStrategy { get; init; }
        public ISupervisorStrategy SupervisorStrategy { get; init; } = Supervision.DefaultStrategy;
        public IDispatcher Dispatcher { get; init; } = Dispatchers.DefaultDispatcher;

        public IList<Func<Receiver, Receiver>> ReceiverMiddleware { get; init; } =
            new List<Func<Receiver, Receiver>>();

        public IList<Func<Sender, Sender>> SenderMiddleware { get; init; } = new List<Func<Sender, Sender>>();
        public Receiver? ReceiverMiddlewareChain { get; init; }
        public Sender? SenderMiddlewareChain { get; init; }

        public IList<Func<IContext, IContext>> ContextDecorator { get; init; } =
            new List<Func<IContext, IContext>>();

        public Func<IContext, IContext>? ContextDecoratorChain { get; init; }

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

        public Props WithProducer(Producer producer) =>
            this with {Producer = producer};

        public Props WithDispatcher(IDispatcher dispatcher) =>
            this with {Dispatcher = dispatcher};

        public Props WithMailbox(MailboxProducer mailboxProducer) =>
            this with { MailboxProducer = mailboxProducer};

        public Props WithContextDecorator(params Func<IContext, IContext>[] contextDecorator)
        {
            var x = ContextDecorator.Concat(contextDecorator).ToList();
            return this with {
                ContextDecorator = x,
                ContextDecoratorChain = x
                    .AsEnumerable()
                    .Reverse()
                    .Aggregate(
                        (Func<IContext, IContext>) DefaultContextDecorator,
                        (inner, outer) => ctx => outer(inner(ctx))
                    )
                };
        }

        public Props WithGuardianSupervisorStrategy(ISupervisorStrategy guardianStrategy) =>
            this with {GuardianStrategy = guardianStrategy};

        public Props WithChildSupervisorStrategy(ISupervisorStrategy supervisorStrategy) =>
            this with {SupervisorStrategy = supervisorStrategy};

        public Props WithReceiverMiddleware(params Func<Receiver, Receiver>[] middleware)
        {
            var x = ReceiverMiddleware.Concat(middleware).ToList();
            return this with {
                ReceiverMiddleware = x,
                ReceiverMiddlewareChain = x.AsEnumerable().Reverse()
                    .Aggregate((Receiver) Middleware.Receive, (inner, outer) => outer(inner))
                };
        }

        public Props WithSenderMiddleware(params Func<Sender, Sender>[] middleware)
        {
            var x = SenderMiddleware.Concat(middleware).ToList();
            return this with {
                SenderMiddleware = x,
                SenderMiddlewareChain = x.AsEnumerable().Reverse()
                    .Aggregate((Sender) Middleware.Sender, (inner, outer) => outer(inner))
                };
        }

        public Props WithSpawner(Spawner spawner) =>
            this with {Spawner = spawner};

        internal PID Spawn(ActorSystem system, string name, PID? parent) => Spawner(system, name, this, parent);
        public static Props FromProducer(Producer producer) => new Props().WithProducer(producer);
        public static Props FromFunc(Receive receive) => FromProducer(() => new FunctionActor(receive));
    }

    public delegate PID Spawner(ActorSystem system, string id, Props props, PID? parent);

    public delegate IActor Producer();

    public delegate IMailbox MailboxProducer();
}