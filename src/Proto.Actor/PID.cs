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
        private Process _p;

        public PID(string address, string id)
        {
            Address = address;
            Id = id;
        }

        internal Process Ref
        {
            get
            {
                var p = _p;
                if (p != null)
                {
                    if (p is LocalProcess lp && lp.IsDead)
                    {
                        _p = null;
                    }
                    return _p;
                }

                var reff = ProcessRegistry.Instance.Get(this);
                if (!(reff is DeadLetterProcess))
                {
                    _p = reff;
                }

                return _p;
            }
        }

        public Task SendAsync(object message)
        {
            var reff = Ref ?? ProcessRegistry.Instance.Get(this);
            return reff.SendUserMessageAsync(this, message);
        }

        public Task SendSystemMessageAsync(object sys)
        {
            var reff = Ref ?? ProcessRegistry.Instance.Get(this);
            return reff.SendSystemMessageAsync(this, sys);
        }

        public Task RequestAsync(object message, PID sender)
        {
            var reff = Ref ?? ProcessRegistry.Instance.Get(this);
            var messageEnvelope = new MessageEnvelope(message,sender,null);
            return reff.SendUserMessageAsync(this, messageEnvelope);
        }
        
        public Task<T> RequestAsync<T>(object message, TimeSpan timeout)
            => RequestAsync(message, new FutureProcess<T>(timeout));

        public Task<T> RequestAsync<T>(object message, CancellationToken cancellationToken)
            => RequestAsync(message, new FutureProcess<T>(cancellationToken));

        public Task<T> RequestAsync<T>(object message)
            => RequestAsync(message, new FutureProcess<T>());

        private async Task<T> RequestAsync<T>(object message, FutureProcess<T> future)
        {
            await RequestAsync(message, future.Pid);
            return await future.Task;
        }

        public Task StopAsync()
        {
            var reff = ProcessRegistry.Instance.Get(this);
            return reff.StopAsync(this);
        }

        public string ToShortString()
        {
            return Address + "/" + Id;
        }
    }
}