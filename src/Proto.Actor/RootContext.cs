// -----------------------------------------------------------------------
//   <copyright file="RootContext.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Proto
{
    public interface IRootContext : ISpawnerContext, ISenderContext, IStopperContext { }

    public class RootContext : IRootContext
    {
        public ActorSystem System { get; }

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

        private Sender? SenderMiddleware { get; set; }
        public MessageHeader Headers { get; private set; }

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
            var parent = props.GuardianStrategy != null ? System.Guardians.GetGuardianPID(props.GuardianStrategy) : null;
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
            var envelope = new MessageEnvelope(message, sender, null);
            Send(target, envelope);
        }

        public Task<T> RequestAsync<T>(PID target, object message, TimeSpan timeout)
            => RequestAsync(target, message, new FutureProcess<T>(System, timeout));

        public Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken)
            => RequestAsync(target, message, new FutureProcess<T>(System, cancellationToken));

        public Task<T> RequestAsync<T>(PID target, object message) => RequestAsync(target, message, new FutureProcess<T>(System));

        public void Stop(PID pid)
        {
            var reff = System.ProcessRegistry.Get(pid);
            reff.Stop(pid);
        }

        public Task StopAsync(PID pid)
        {
            var future = new FutureProcess<object>(System);

            pid.SendSystemMessage(System, new Watch(future.Pid));
            Stop(pid);

            return future.Task;
        }

        public void Poison(PID pid) => pid.SendUserMessage(System, new PoisonPill());

        public Task PoisonAsync(PID pid)
        {
            var future = new FutureProcess<object>(System);

            pid.SendSystemMessage(System, new Watch(future.Pid));
            Poison(pid);

            return future.Task;
        }

        public RootContext WithHeaders(MessageHeader headers) => Copy(c => c.Headers = headers);

        public RootContext WithSenderMiddleware(params Func<Sender, Sender>[] middleware)
            => Copy(
                c =>
                {
                    SenderMiddleware = middleware.Reverse()
                        .Aggregate((Sender) DefaultSender, (inner, outer) => outer(inner));
                }
            );

        private RootContext Copy(Action<RootContext> mutator)
        {
            var copy = new RootContext(System)
            {
                SenderMiddleware = SenderMiddleware,
                Headers = Headers
            };
            mutator(copy);
            return copy;
        }

        private Task DefaultSender(ISenderContext context, PID target, MessageEnvelope message)
        {
            target.SendUserMessage(context.System, message);
            return Proto.Actor.Done;
        }

        private Task<T> RequestAsync<T>(PID target, object message, FutureProcess<T> future)
        {
            var messageEnvelope = new MessageEnvelope(message, future.Pid, null);
            SendUserMessage(target, messageEnvelope);

            return future.Task;
        }

        private void SendUserMessage(PID target, object message)
        {
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
