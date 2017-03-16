// -----------------------------------------------------------------------
//  <copyright file="Futures.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto
{
    public class FutureProcess<T> : Process
    {
        private readonly TaskCompletionSource<T> _tcs;
        private readonly CancellationTokenSource _cts;

        public FutureProcess(TimeSpan timeout) : this(new CancellationTokenSource(timeout)) { }
        public FutureProcess(CancellationToken cancellationToken) : this(CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)) { }
        public FutureProcess() : this((CancellationTokenSource)null) { }

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
            PID = pid;

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

        public PID PID { get; }
        public Task<T> Task { get; }

        public override void SendUserMessage(PID pid, object message, PID sender)
        {
            if (message is T || message == null)
            {
                if (_cts != null && _cts.IsCancellationRequested) return;

                _tcs.TrySetResult((T)message);
                pid.Stop();
            }            
            else
            {
                throw new InvalidOperationException($"Unexpected message.  Was type {message.GetType()} but expected {typeof(T)}");
            }

        }

        public override void SendSystemMessage(PID pid, object message)
        {
        }
    }
}