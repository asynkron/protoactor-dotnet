// -----------------------------------------------------------------------
//  <copyright file="PID.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto
{
    public partial class PID
    {
        public PID(string address, string id)
        {
            Address = address;
            Id = id;
        }

        internal Process Ref { get; set; }

        public void Tell(object message)
        {
            var reff = Ref ?? ProcessRegistry.Instance.Get(this);
            reff.SendUserMessage(this, message, null);
        }

        public void SendSystemMessage(object sys)
        {
            var reff = Ref ?? ProcessRegistry.Instance.Get(this);
            reff.SendSystemMessage(this, sys);
        }

        public void Request(object message, PID sender)
        {
            var reff = Ref ?? ProcessRegistry.Instance.Get(this);
            reff.SendUserMessage(this, message, sender);
        }


        public Task<T> RequestAsync<T>(object message, TimeSpan timeout)
            => RequestAsync(message, new FutureProcess<T>(timeout));

        public Task<T> RequestAsync<T>(object message, CancellationToken cancellationToken)
            => RequestAsync(message, new FutureProcess<T>(cancellationToken));

        public Task<T> RequestAsync<T>(object message)
            => RequestAsync(message, new FutureProcess<T>());

        private Task<T> RequestAsync<T>(object message, FutureProcess<T> future)
        {
            Request(message, future.PID);
            return future.Task;
        }

        public void Stop()
        {
            var reff = ProcessRegistry.Instance.Get(this);
            reff.Stop(this);
        }

        public string ToShortString()
        {
            return Address + "/" + Id;
        }
    }
}