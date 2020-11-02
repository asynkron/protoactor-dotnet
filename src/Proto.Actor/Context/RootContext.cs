// -----------------------------------------------------------------------
//   <copyright file="RootContext.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

namespace Proto
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Future;
    using JetBrains.Annotations;

    public interface IRootContext : ISpawnerContext, ISenderContext, IStopperContext
    {
    }

    [PublicAPI]
    public record RootContext : IRootContext
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
        public MessageHeader Headers { get; init; }

        public PID? Parent => null;
        public PID? Self => null;
        PID? IInfoContext.Sender => null;
        public IActor? Actor => null;

        public PID Spawn(Props props)
        {
            var name = System.ProcessRegistry.NextId();
            return SpawnNamed(props, name);
        }

        public PID SpawnNamed(Props props, string name)
        {
            var parent = props.GuardianStrategy != null
                ? System.Guardians.GetGuardianPid(props.GuardianStrategy)
                : null;
            return props.Spawn(System, name, parent);
        }

        public PID SpawnPrefix(Props props, string prefix)
        {
            var name = prefix + System.ProcessRegistry.NextId();
            return SpawnNamed(props, name);
        }

        public object? Message => null;

        public void Send(PID target, object message) => SendUserMessage(target, message);

        public void Request(PID target, object message) => SendUserMessage(target, message);

        public void Request(PID target, object message, PID? sender)
        {
            var envelope = new MessageEnvelope(message, sender);
            Send(target, envelope);
        }

        public Task<T> RequestAsync<T>(PID target, object message, TimeSpan timeout)
            => RequestAsync<T>(target, message, new FutureProcess(System, timeout));

        public Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken)
            => RequestAsync<T>(target, message, new FutureProcess(System, cancellationToken));

        public Task<T> RequestAsync<T>(PID target, object message) =>
            RequestAsync<T>(target, message, new FutureProcess(System));

        public void Stop(PID? pid)
        {
            if (pid == null)
            {
                return;
            }

            var reff = System.ProcessRegistry.Get(pid);
            reff.Stop(pid);
        }

        public Task StopAsync(PID pid)
        {
            var future = new FutureProcess(System);

            pid.SendSystemMessage(System, new Watch(future.Pid));
            Stop(pid);

            return future.Task;
        }

        public void Poison(PID pid) => pid.SendUserMessage(System, PoisonPill.Instance);

        public Task PoisonAsync(PID pid)
        {
            var future = new FutureProcess(System);

            pid.SendSystemMessage(System, new Watch(future.Pid));
            Poison(pid);

            return future.Task;
        }

        public RootContext WithHeaders(MessageHeader headers) =>
            this with {Headers = headers};

        public RootContext WithSenderMiddleware(params Func<Sender, Sender>[] middleware) =>
            this with {
                SenderMiddleware = middleware.Reverse()
                    .Aggregate((Sender) DefaultSender, (inner, outer) => outer(inner))
                };
        

        private Task DefaultSender(ISenderContext context, PID target, MessageEnvelope message)
        {
            target.SendUserMessage(context.System, message);
            return Task.CompletedTask;
        }

        private async Task<T> RequestAsync<T>(PID target, object message, FutureProcess future)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var messageEnvelope = new MessageEnvelope(message, future.Pid);
            SendUserMessage(target, messageEnvelope);
            var result = await future.Task;
            switch (result)
            {
                case DeadLetterResponse _:
                    throw new DeadLetterException(target);
                case null:
                case T _:
                    return (T) result!;
                default:
                    throw new InvalidOperationException(
                        $"Unexpected message. Was type {result.GetType()} but expected {typeof(T)}"
                    );
            }
        }

        private void SendUserMessage(PID target, object message)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (SenderMiddleware != null)
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
    }
}