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
    public interface IRootContext : ISpawnerContext, ISenderContext, IStopperContext
    {
    }

    public class RootContext : IRootContext
    {
        private readonly ActorSystem _system;

        public RootContext(ActorSystem system)
        {
            _system = system;
            SenderMiddleware = null;
            Headers = MessageHeader.Empty;
        }

        public RootContext(ActorSystem system, MessageHeader messageHeader, params Func<Sender, Sender>[] middleware)
        {
            _system = system;
            SenderMiddleware = middleware.Reverse()
                .Aggregate((Sender)DefaultSender, (inner, outer) => outer(inner));
            Headers = messageHeader;
        }

        private Sender SenderMiddleware { get; set; }
        public MessageHeader Headers { get; private set; }

        public PID? Parent => null;
        public PID? Self => null;
        PID? IInfoContext.Sender => null;
        public IActor? Actor => null;

        public PID Spawn(Props props)
        {
            var name = _system.ProcessRegistry.NextId();
            return SpawnNamed(props, name);
        }

        public PID SpawnNamed(Props props, string name)
        {
            var parent = props.GuardianStrategy != null ? _system.Guardians.GetGuardianPID(props.GuardianStrategy) : null;
            return props.Spawn(_system, name, parent);
        }

        public PID SpawnPrefix(Props props, string prefix)
        {
            var name = prefix + _system.ProcessRegistry.NextId();
            return SpawnNamed(props, name);
        }

        public object Message => null;

        public void Send(PID target, object message)
            => SendUserMessage(target, message);

        public void Request(PID target, object message)
            => SendUserMessage(target, message);

        public void Request(PID target, object message, PID sender)
        {
            var envelope = new MessageEnvelope(message, sender, null);
            Send(target, envelope);
        }

        public Task<T> RequestAsync<T>(PID target, object message, TimeSpan timeout)
            => RequestAsync(target, message, new FutureProcess<T>(_system, timeout));

        public Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken)
            => RequestAsync(target, message, new FutureProcess<T>(_system, cancellationToken));

        public Task<T> RequestAsync<T>(PID target, object message)
            => RequestAsync(target, message, new FutureProcess<T>(_system));

        public void Stop(PID pid)
        {
            var reff = _system.ProcessRegistry.Get(pid);
            reff.Stop(pid);
        }

        public Task StopAsync(PID pid)
        {
            var future = new FutureProcess<object>(_system);

            pid.SendSystemMessage(_system, new Watch(future.Pid));
            Stop(pid);

            return future.Task;
        }

        public void Poison(PID pid) => pid.SendUserMessage(_system, new PoisonPill());

        public Task PoisonAsync(PID pid)
        {
            var future = new FutureProcess<object>(_system);

            pid.SendSystemMessage(_system, new Watch(future.Pid));
            Poison(pid);

            return future.Task;
        }

        public RootContext WithHeaders(MessageHeader headers) => Copy(c => c.Headers = headers);

        public RootContext WithSenderMiddleware(params Func<Sender, Sender>[] middleware) => Copy(c =>
            {
                SenderMiddleware = middleware.Reverse()
                    .Aggregate((Sender)DefaultSender, (inner, outer) => outer(inner));
            }
        );


        private RootContext Copy(Action<RootContext> mutator)
        {
            var copy = new RootContext(_system)
            {
                SenderMiddleware = SenderMiddleware,
                Headers = Headers
            };
            mutator(copy);
            return copy;
        }


        private Task DefaultSender(ActorSystem system, ISenderContext context, PID target, MessageEnvelope message)
        {
            target.SendUserMessage(system, message);
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
                SenderMiddleware(_system, this, target, MessageEnvelope.Wrap(message));
            }
            else
            {
                //fast path, 0 alloc
                target.SendUserMessage(_system, message);
            }
        }
    }
}