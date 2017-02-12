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
            => RequestAsync<T>(message, new CancellationTokenSource(timeout));

        public Task<T> RequestAsync<T>(object message, CancellationToken cancellationToken)
            => RequestAsync<T>(message, CancellationTokenSource.CreateLinkedTokenSource(cancellationToken));

        public Task<T> RequestAsync<T>(object message)
            => RequestAsync<T>(message, null);

        private Task<T> RequestAsync<T>(object message, CancellationTokenSource cts = null)
        {
            var tsc = new TaskCompletionSource<T>();

            var reff = (cts == null) ? new FutureProcess<T>(tsc) : new FutureProcess<T>(tsc, cts.Token);

            var name = ProcessRegistry.Instance.NextId();
            var (pid, absent) = ProcessRegistry.Instance.TryAdd(name, reff);
            if (!absent)
            {
                throw new ProcessNameExistException(name);
            }
            Request(message, pid);


            if (cts == null) return tsc.Task;
            else
            {
                var resultTask = tsc.Task;
                var resultRunner = tsc.Task.ContinueWith(t => cts.Cancel(), cts.Token);
                var timeoutRunner = Task.Delay(-1, cts.Token).ContinueWith(t => { if (!resultTask.IsCompleted) pid.Stop(); });

                return
                    Task.WhenAny(resultRunner, timeoutRunner)
                    .ContinueWith(t =>
                    {
                        if (resultTask.IsCompleted) return resultTask.Result;
                        else throw new TimeoutException("Request didn't receive any Response within the expected time.");
                    });
            }
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