// -----------------------------------------------------------------------
//   <copyright file="RootContext.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Proto
{
    public class ActorClient : ISenderContext    {
        private readonly Sender _senderMiddleware;

        public ActorClient(MessageHeader messageHeader, params Func<Sender, Sender>[] middleware)
        {
            _senderMiddleware = middleware.Reverse()
                    .Aggregate((Sender)DefaultSender, (inner, outer) => outer(inner));
            Headers = messageHeader;
        }

        private Task DefaultSender(ISenderContext context,PID target, MessageEnvelope message)
        {
            return target.SendAsync(message);
        }

        public Task SendAsync(PID target, object message)
        {
            if (_senderMiddleware != null)
            {
                if (message is MessageEnvelope messageEnvelope)
                {
                    //Request based middleware
                    return _senderMiddleware(this, target, messageEnvelope);
                }
                else
                {
                    //tell based middleware
                    return _senderMiddleware(this, target, new MessageEnvelope(message,null,null));
                }
            }
            else
            {
                //Default path
                return target.SendAsync(message);
            }
        }

        public Task RequestAsync(PID target, object message,PID sender)
        {
            var envelope = new MessageEnvelope(message,sender,null);
            return SendAsync(target,envelope);
        }

        public Task<T> RequestAsync<T>(PID target, object message, TimeSpan timeout)
        {
            throw new NotImplementedException();
        }

        public Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<T> RequestAsync<T>(PID target, object message)
        {
            throw new NotImplementedException();
        }

        public object Message => null;
        public MessageHeader Headers { get; }
    }
}
