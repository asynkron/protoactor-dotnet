// -----------------------------------------------------------------------
//   <copyright file="PID.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto
{
    public partial class PID
    {
        private Process _process;

        public PID(string address, string id)
        {
            Address = address;
            Id = id;
        }

        internal PID(string address, string id, Process process) : this(address, id)
        {
            _process = process;
        }

        internal Process Ref
        {
            get
            {
                var p = _process;
                if (p != null)
                {
                    if (p is LocalProcess lp && lp.IsDead)
                    {
                        _process = null;
                    }
                    return _process;
                }

                var reff = ProcessRegistry.Instance.Get(this);
                if (!(reff is DeadLetterProcess))
                {
                    _process = reff;
                }

                return _process;
            }
        }

        public void Tell(object message)
        {
            var reff = Ref ?? ProcessRegistry.Instance.Get(this);
            reff.SendUserMessage(this, message);
        }

        public void SendSystemMessage(object sys)
        {
            var reff = Ref ?? ProcessRegistry.Instance.Get(this);
            reff.SendSystemMessage(this, sys);
        }

        public void Request(object message, PID sender)
        {
            var reff = Ref ?? ProcessRegistry.Instance.Get(this);
            var messageEnvelope = new MessageEnvelope(message,sender,null);
            reff.SendUserMessage(this, messageEnvelope);
        }

        public Task<T> RequestAsync<T>(object message, TimeSpan timeout)
            => RequestAsync(message, new FutureProcess<T>(timeout));

        public Task<T> RequestAsync<T>(object message, CancellationToken cancellationToken)
            => RequestAsync(message, new FutureProcess<T>(cancellationToken));

        public Task<T> RequestAsync<T>(object message)
            => RequestAsync(message, new FutureProcess<T>());

        private Task<T> RequestAsync<T>(object message, FutureProcess<T> future)
        {
            Request(message, future.Pid);
            return future.Task;
        }

        /// <summary> Stop will tell actor to stop immediately, regardless of existing user messages in mailbox. </summary>
        public void Stop()
        {
            var reff = ProcessRegistry.Instance.Get(this);
            reff.Stop(this);
        }

        /// <summary> StopAsync will tell and wait actor to stop immediately, regardless of existing user messages in mailbox. </summary>
        public Task StopAsync()
        {
            var future = new FutureProcess<object>();

            SendSystemMessage(new Watch(future.Pid));
            Stop();

            return future.Task;
        }

        /// <summary> Poison will tell actor to stop after processing current user messages in mailbox. </summary>
        public void Poison() => Tell(new PoisonPill());

        /// <summary> PoisonAsync will tell and wait actor to stop after processing current user messages in mailbox. </summary>
        public Task PoisonAsync()
        {
            var future = new FutureProcess<object>();

            SendSystemMessage(new Watch(future.Pid));
            Poison();

            return future.Task;
        }

        public string ToShortString()
        {
            return Address + "/" + Id;
        }
    }
}