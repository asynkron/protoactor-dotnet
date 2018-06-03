// -----------------------------------------------------------------------
//   <copyright file="ISenderContext.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto
{
    public interface ISenderContext
    {
        object Message { get; }

        MessageHeader Headers { get; }

        void Send(PID target, object message);

        void Request(PID target, object message);
        Task<T> RequestAsync<T>(PID target, object message, TimeSpan timeout);
        Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken);
        Task<T> RequestAsync<T>(PID target, object message);
    }
}