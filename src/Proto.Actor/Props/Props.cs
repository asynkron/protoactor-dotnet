// -----------------------------------------------------------------------
// <copyright file="Props.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using Proto.Context;
using Proto.Mailbox;

namespace Proto;

/// <summary>
/// Use Props to specify how a new actor should be created.
/// </summary>
[PublicAPI]
public sealed record Props
{
    private static IActor NullProducer(ActorSystem _, IContext __) => null!;
    
    public static readonly Props Empty = new();

    /// <summary>
    /// Delegate used to create the actor.
    /// </summary>
    public ProducerWithSystemAndContext Producer { get; init; } = NullProducer;
    
    /// <summary>
    /// Deletegate used to create the mailbox.
    /// </summary>
    public MailboxProducer MailboxProducer { get; init; } = () => UnboundedMailbox.Create();
    
    /// <summary>
    /// Used when actor is spawned at the root of the system. A guardian process will be created to handle failures of this actor,
    /// according to the supervision strategy specified here.
    /// </summary>
    public ISupervisorStrategy? GuardianStrategy { get; init; }
    
    /// <summary>
    /// Supervision strategy for handling failures in actor's children.
    /// </summary>
    public ISupervisorStrategy SupervisorStrategy { get; init; } = Supervision.DefaultStrategy;
    
    /// <summary>
    /// Dispatcher to be used by the actor's mailbox.
    /// </summary>
    public IDispatcher Dispatcher { get; init; } = Dispatchers.DefaultDispatcher;

    /// <summary>
    /// Middleware used when receiving a message
    /// </summary>
    public ImmutableList<Func<Receiver, Receiver>> ReceiverMiddleware { get; init; } =
        ImmutableList<Func<Receiver, Receiver>>.Empty;

    /// <summary>
    /// Middleware used when sending a message
    /// </summary>
    public ImmutableList<Func<Sender, Sender>> SenderMiddleware { get; init; } =
        ImmutableList<Func<Sender, Sender>>.Empty;

    public Receiver? ReceiverMiddlewareChain { get; init; }
    public Sender? SenderMiddlewareChain { get; init; }

    /// <summary>
    /// List of decorators for the actor context
    /// </summary>
    public ImmutableList<Func<IContext, IContext>> ContextDecorator { get; init; } =
        ImmutableList<Func<IContext, IContext>>.Empty;

    public Func<IContext, IContext>? ContextDecoratorChain { get; init; }

    /// <summary>
    /// Delegate that creates the actor and wires it with context and mailbox.
    /// </summary>
    public Spawner Spawner { get; init; } = DefaultSpawner;

    private static IContext DefaultContextDecorator(IContext context) => context;

    public static PID DefaultSpawner(ActorSystem system, string name, Props props, PID? parent, Action<IContext>? callback)
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
        callback?.Invoke(ctx);
        mailbox.RegisterHandlers(ctx, dispatcher);
        mailbox.PostSystemMessage(Started.Instance);
            
        //finally, start the mailbox and make the actor consume messages
        mailbox.Start();

        return self;
    }

    public Props WithProducer(Producer producer) =>
        this with {Producer = (_,_) => producer()};

    public Props WithProducer(ProducerWithSystem producer) =>
        this with {Producer = (system, _) => producer(system)};
        
    public Props WithProducer(ProducerWithSystemAndContext producer) =>
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

    internal PID Spawn(ActorSystem system, string name, PID? parent, Action<IContext>? callback=null) => Spawner(system, name, this, parent, callback);

    /// <summary>
    /// Props that spawn actors by calling the provided producer delegate.
    /// </summary>
    /// <param name="producer">Returns a new instance of the actor</param>
    /// <returns></returns>
    /// <example>
    /// <code>
    /// var props = Props.FromProducer(() => new MyActor());
    /// </code>
    /// </example>
    public static Props FromProducer(Producer producer) => Empty.WithProducer(_ => producer());

    /// <summary>
    /// Props that spawn actors by calling the provided producer delegate.
    /// </summary>
    /// <param name="producer">Returns a new instance of the actor. Gets <see cref="ActorSystem"/> as a parameter</param>
    /// <returns></returns>
    public static Props FromProducer(ProducerWithSystem producer) => Empty.WithProducer(producer);

    /// <summary>
    /// Props that spawn actors based on provided <see cref="Receive"> delegate implementation. Useful when you don't want to create an actor class.
    /// The Receive delegate will be wrapped in a <see cref="FunctionActor"/> instance.
    /// </summary>
    /// <param name="receive"></param>
    /// <returns></returns>
    /// <example>
    /// <code>
    /// var props = Props.FromFunc(ctx => {
    ///     if (ctx.Message is Hello msg)
    ///         Console.WriteLine($"Hello {msg.Name}");
    ///     return Task.CompletedTask;
    /// });
    /// </code>
    /// </example>
    public static Props FromFunc(Receive receive) => FromProducer(() => new FunctionActor(receive));
}