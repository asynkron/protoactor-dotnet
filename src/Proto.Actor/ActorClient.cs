// -----------------------------------------------------------------------
//   <copyright file="ActorClient.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Proto
{   
    public class ActorClient : ISenderContext
    {
        public static readonly ActorClient DefaultContext = new ActorClient(MessageHeader.EmptyHeader);
        private readonly Sender _senderMiddleware;

        public ActorClient(MessageHeader messageHeader, params Func<Sender, Sender>[] middleware)
        {
            _senderMiddleware = middleware.Reverse()
                    .Aggregate((Sender)DefaultSender, (inner, outer) => outer(inner));
            Headers = messageHeader;
        }

        public object Message => null;

        public MessageHeader Headers { get; }

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
            if (_senderMiddleware != null)
            {
                if (message is MessageEnvelope messageEnvelope)
                {
                    //Request based middleware
                    _senderMiddleware(this, target, messageEnvelope);
                }
                else
                {
                    //tell based middleware
                    _senderMiddleware(this, target, new MessageEnvelope(message, null, null));
                }
            }
            else
            {
                //Default path
                target.SendUserMessage(message);
            }
        }
    }
}
