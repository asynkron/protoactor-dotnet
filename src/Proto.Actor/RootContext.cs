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
    public interface IRootContext : ISpawnContext, ISenderContext
    {
    }
    
    public class RootContext : IRootContext
    {
        public static readonly RootContext Empty = new RootContext();
        private Sender SenderMiddleware { get; set; }
        public MessageHeader Headers { get; private set; }


        public PID Spawn(Props props)
        {
            var name = ProcessRegistry.Instance.NextId();
            return SpawnNamed(props, name);
        }

        public PID SpawnNamed(Props props, string name)
        {
            var parent = props.GuardianStrategy != null ? Guardians.GetGuardianPID(props.GuardianStrategy) : null;
            return props.Spawn(name, parent);
        }

        public PID SpawnPrefix(Props props, string prefix)
        {
            var name = prefix + ProcessRegistry.Instance.NextId();
            return SpawnNamed(props, name);
        }

        public RootContext()
        {
            SenderMiddleware = null;
            Headers = MessageHeader.Empty;
        }

        public RootContext(MessageHeader messageHeader, params Func<Sender, Sender>[] middleware)
        {
            SenderMiddleware = middleware.Reverse()
                .Aggregate((Sender) DefaultSender, (inner, outer) => outer(inner));
            Headers = messageHeader;
        }

        public RootContext WithHeaders(MessageHeader headers) => Copy(c => c.Headers = headers);
        public RootContext WithSenderMiddleware(params Func<Sender, Sender>[] middleware) => Copy(c =>
        {
            SenderMiddleware = middleware.Reverse()
                .Aggregate((Sender) DefaultSender, (inner, outer) => outer(inner));
        });


        private RootContext Copy(Action<RootContext> mutator)
        {
            var copy = new RootContext
            {
                SenderMiddleware = SenderMiddleware,
                Headers = Headers
            };
            mutator(copy);
            return copy;
        }

        public object Message => null;


        private Task DefaultSender(ISenderContext context, PID target, MessageEnvelope message)
        {
            target.SendUserMessage(message);
            return Actor.Done;
        }

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
            => RequestAsync(target, message, new FutureProcess<T>(timeout));

        public Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken)
            => RequestAsync(target, message, new FutureProcess<T>(cancellationToken));

        public Task<T> RequestAsync<T>(PID target, object message)
            => RequestAsync(target, message, new FutureProcess<T>());

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
                if (message is MessageEnvelope messageEnvelope)
                {
                    //Request based middleware
                    SenderMiddleware(this, target, messageEnvelope);
                }
                else
                {
                    //tell based middleware
                    SenderMiddleware(this, target, new MessageEnvelope(message, null, null));
                }
                return;
            }
            //Default path
            target.SendUserMessage(message);
        }
    }
}
