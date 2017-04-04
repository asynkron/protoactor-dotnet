// -----------------------------------------------------------------------
//   <copyright file="RootContext.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto
{
    public interface IOutboundContext
    {
        void Tell(PID target, object message);
        void Request(PID target, object message);
        Task<T> RequestAsync<T>(PID target, object message, TimeSpan timeout);
        Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken);
        Task<T> RequestAsync<T>(PID target, object message);
    }
    public class RootContext : IOutboundContext
    {
        private readonly Sender _senderMiddleware;

        public RootContext(Sender senderMiddleware)
        {
            _senderMiddleware = senderMiddleware;
        }

        public void Tell(PID pid, object message)
        {
            
        }

        public void Request(PID target, object message)
        {
            throw new NotImplementedException();
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
    }
}
