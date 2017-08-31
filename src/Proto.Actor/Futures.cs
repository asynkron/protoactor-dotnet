// -----------------------------------------------------------------------
//   <copyright file="Futures.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto
{
    internal class FutureProcess<T> : Process
    {
        private readonly CancellationTokenSource _cts;
        private readonly TaskCompletionSource<T> _tcs;

        internal FutureProcess(TimeSpan timeout) : this(new CancellationTokenSource(timeout)) { }
        internal FutureProcess(CancellationToken cancellationToken) : this(CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)) { }
        internal FutureProcess() : this(null) { }

        FutureProcess(CancellationTokenSource cts)
        {
            _tcs = new TaskCompletionSource<T>();
            _cts = cts;

            var name = ProcessRegistry.Instance.NextId();
            var (pid, absent) = ProcessRegistry.Instance.TryAdd(name, this);
            if (!absent)
            {
                throw new ProcessNameExistException(name);
            }
            Pid = pid;

            if (cts != null)
            {
                System.Threading.Tasks.Task.Delay(-1, cts.Token)
                    .ContinueWith(t =>
                    {
                        if (!_tcs.Task.IsCompleted)
                        {
                            _tcs.TrySetException(new TimeoutException("Request didn't receive any Response within the expected time."));
                            pid.Stop();
                        }
                    });
            }

            Task = _tcs.Task;
        }

        public PID Pid { get; }
        public Task<T> Task { get; }

        protected internal override void SendUserMessage(PID pid, object message)
        {
            var env = MessageEnvelope.Unwrap(message);
            

            if (env.message is T || message == null)
            {
                if (_cts != null && _cts.IsCancellationRequested)
                {
                    return;
                }

                _tcs.TrySetResult((T)env.message);
                pid.Stop();
            }            
            else
            {
                throw new InvalidOperationException($"Unexpected message.  Was type {env.message.GetType()} but expected {typeof(T)}");
            }

        }

        protected internal override void SendSystemMessage(PID pid, object message)
        {
        }
    }
}