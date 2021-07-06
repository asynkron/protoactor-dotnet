// -----------------------------------------------------------------------
// <copyright file="Props.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using Proto.Context;
using Proto.Mailbox;

namespace Proto
{
    [PublicAPI]
    public sealed record Props
    {
        public static readonly Props Empty = new();

        public ProducerWithSystem Producer { get; init; } = _ => null!;
        public MailboxProducer MailboxProducer { get; init; } = () => UnboundedMailbox.Create();
        public ISupervisorStrategy? GuardianStrategy { get; init; }
        public ISupervisorStrategy SupervisorStrategy { get; init; } = Supervision.DefaultStrategy;
        public IDispatcher Dispatcher { get; init; } = Dispatchers.DefaultDispatcher;

        public ImmutableList<Func<Receiver, Receiver>> ReceiverMiddleware { get; init; } =
            ImmutableList<Func<Receiver, Receiver>>.Empty;

        public ImmutableList<Func<Sender, Sender>> SenderMiddleware { get; init; } =
            ImmutableList<Func<Sender, Sender>>.Empty;

        public Receiver? ReceiverMiddlewareChain { get; init; }
        public Sender? SenderMiddlewareChain { get; init; }

        public ImmutableList<Func<IContext, IContext>> ContextDecorator { get; init; } =
            ImmutableList<Func<IContext, IContext>>.Empty;

        public ImmutableList<Action<IContext>> OnInit { get; init; } = ImmutableList<Action<IContext>>.Empty;

        public Func<IContext, IContext>? ContextDecoratorChain { get; init; }

        public Spawner Spawner { get; init; } = DefaultSpawner;

        private static IContext DefaultContextDecorator(IContext context) => context;

        public static PID DefaultSpawner(ActorSystem system, string name, Props props, PID? parent)
        {
            //Ordering is important here
            //first we create a mailbox and attach it to a process
            props = system.ConfigureProps(props);
            var mailbox = props.MailboxProducer();
            var dispatcher = props.Dispatcher;
            var process = new ActorProcess(system, mailbox);
            
            //then we register it to the process registry
            var (self, absent) = system.ProcessRegistry.TryAdd(name, process);
            //if this fails we exit and the process and mailbox is Garbage Collected
            if (!absent) throw new ProcessNameExistException(name, self);
            
            //if successful, we create the actor and attach it to the mailbox
            var ctx = ActorContext.Setup(system, props, parent, self, mailbox);
            Initialize(props, ctx);
            mailbox.RegisterHandlers(ctx, dispatcher);
            mailbox.PostSystemMessage(Started.Instance);
            
            //finally, start the mailbox and make the actor consume messages
            mailbox.Start();

            return self;
        }

        private static void Initialize(Props props, ActorContext ctx)
        {
            foreach (var init in props.OnInit)
            {
                init(ctx);
            }
        }

        public Props WithProducer(Producer producer) =>
            this with {Producer = _ => producer()};

        public Props WithProducer(ProducerWithSystem producer) =>
            this with {Producer = producer};

        public Props WithDispatcher(IDispatcher dispatcher) =>
            this with {Dispatcher = dispatcher};

        public Props WithMailbox(MailboxProducer mailboxProducer) =>
            this with {MailboxProducer = mailboxProducer};

        public Props WithContextDecorator(params Func<IContext, IContext>[] contextDecorator)
        {
            var x = ContextDecorator.AddRange(contextDecorator);
            return this with
            {
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

        public Props WithOnInit(params Action<IContext>[] callback) => this with
        {
            OnInit = OnInit.AddRange(callback),
        };

        public Props WithGuardianSupervisorStrategy(ISupervisorStrategy guardianStrategy) =>
            this with {GuardianStrategy = guardianStrategy};

        public Props WithChildSupervisorStrategy(ISupervisorStrategy supervisorStrategy) =>
            this with {SupervisorStrategy = supervisorStrategy};

        public Props WithReceiverMiddleware(params Func<Receiver, Receiver>[] middleware)
        {
            var x = ReceiverMiddleware.AddRange(middleware);
            return this with
            {
                ReceiverMiddleware = x,
                ReceiverMiddlewareChain = x.AsEnumerable().Reverse()
                    .Aggregate((Receiver) Middleware.Receive, (inner, outer) => outer(inner))
            };
        }

        public Props WithSenderMiddleware(params Func<Sender, Sender>[] middleware)
        {
            var x = SenderMiddleware.AddRange(middleware);
            return this with
            {
                SenderMiddleware = x,
                SenderMiddlewareChain = x.AsEnumerable().Reverse()
                    .Aggregate((Sender) Middleware.Sender, (inner, outer) => outer(inner))
            };
        }

        public Props WithSpawner(Spawner spawner) =>
            this with {Spawner = spawner};

        internal PID Spawn(ActorSystem system, string name, PID? parent) => Spawner(system, name, this, parent);

        public static Props FromProducer(Producer producer) => Empty.WithProducer(s => producer());

        public static Props FromProducer(ProducerWithSystem producer) => Empty.WithProducer(producer);

        public static Props FromFunc(Receive receive) => FromProducer(() => new FunctionActor(receive));
    }
}