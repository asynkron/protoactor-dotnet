// -----------------------------------------------------------------------
// <copyright file="RootContext.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Proto.Future;
using Proto.Utils;

namespace Proto
{
    public interface IRootContext : ISpawnerContext, ISenderContext, IStopperContext
    {
    }

    [PublicAPI]
    public sealed record RootContext : IRootContext
    {
        public RootContext(ActorSystem system)
        {
            System = system;
            SenderMiddleware = null;
            Headers = MessageHeader.Empty;
        }

        public RootContext(ActorSystem system, MessageHeader? messageHeader, params Func<Sender, Sender>[] middleware)
        {
            System = system;

            SenderMiddleware = middleware.Reverse()
                .Aggregate((Sender) DefaultSender, (inner, outer) => outer(inner));
            Headers = messageHeader ?? MessageHeader.Empty;
        }

        private Sender? SenderMiddleware { get; init; }
        public ActorSystem System { get; }
        private TypeDictionary<object, RootContext> Store { get; } = new(0, 1);

        public T? Get<T>() => (T?) Store.Get<T>();

        public void Set<T, TI>(TI obj) where TI : T => Store.Add<T>(obj!);

        public void Remove<T>() => Store.Remove<T>();

        public MessageHeader Headers { get; init; }

        public PID? Parent => null;
        public PID? Self => null;
        PID? IInfoContext.Sender => null;
        public IActor? Actor => null;

        public PID SpawnNamed(Props props, string name)
        {
            var parent = props.GuardianStrategy is not null
                ? System.Guardians.GetGuardianPid(props.GuardianStrategy)
                : null;
            return props.Spawn(System, name, parent);
        }

        public object? Message => null;

        public void Send(PID target, object message) => SendUserMessage(target, message);

        public void Request(PID target, object message, PID? sender)
        {
            var envelope = new MessageEnvelope(message, sender);
            Send(target, envelope);
        }

        //why does this method exist here and not as an extension?
        //because DecoratorContexts needs to go this way if we want to intercept this method for the context
        public Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken)
            => SenderContextExtensions.RequestAsync<T>(this, target, message, cancellationToken);

        public void Stop(PID? pid)
        {
            if (pid is null) return;

            var reff = System.ProcessRegistry.Get(pid);
            reff.Stop(pid);
        }

        public Task StopAsync(PID pid)
        {
            var future = System.Future.Get();
            pid.SendSystemMessage(System, new Watch(future.Pid));
            Stop(pid);

            return future.Task;
        }

        public void Poison(PID pid) => pid.SendUserMessage(System, PoisonPill.Instance);

        public Task PoisonAsync(PID pid) => RequestAsync<Terminated>(pid, PoisonPill.Instance, CancellationToken.None);

        public RootContext WithHeaders(MessageHeader headers) =>
            this with {Headers = headers};

        public RootContext WithSenderMiddleware(params Func<Sender, Sender>[] middleware) =>
            this with
            {
                SenderMiddleware = middleware.Reverse()
                    .Aggregate((Sender) DefaultSender, (inner, outer) => outer(inner))
            };

        private Task DefaultSender(ISenderContext context, PID target, MessageEnvelope message)
        {
            target.SendUserMessage(context.System, message);
            return Task.CompletedTask;
        }

        private void SendUserMessage(PID target, object message)
        {
            if (target is null) throw new ArgumentNullException(nameof(target));

            if (SenderMiddleware is not null)
            {
                //slow path
                SenderMiddleware(this, target, MessageEnvelope.Wrap(message));
            }
            else
            {
                //fast path, 0 alloc
                target.SendUserMessage(System, message);
            }
        }

        public IFuture GetFuture() => System.Future.Get();
    }
}