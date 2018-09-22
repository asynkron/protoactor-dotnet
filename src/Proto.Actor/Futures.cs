// -----------------------------------------------------------------------
//   <copyright file="Futures.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
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

        internal FutureProcess(TimeSpan timeout) : this(new CancellationTokenSource(timeout))
        {
        }

        internal FutureProcess(CancellationToken cancellationToken) : this(
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
        }

        internal FutureProcess() : this(null)
        {
        }

        private FutureProcess(CancellationTokenSource cts)
        {
            _tcs = new TaskCompletionSource<T>();
            _cts = cts;

            var name = ProcessRegistry.Instance.NextId();
            var (pid, absent) = ProcessRegistry.Instance.TryAdd(name, this);
            if (!absent)
            {
                throw new ProcessNameExistException(name, pid);
            }

            Pid = pid;

            if (cts != null)
            {
                //TODO: I don't think this is correct, there is probably a more kosher way to do this
                System.Threading.Tasks.Task.Delay(-1, cts.Token)
                    .ContinueWith(t =>
                    {
                        if (_tcs.Task.IsCompleted)
                        {
                            return;
                        }

                        _tcs.TrySetException(
                            new TimeoutException("Request didn't receive any Response within the expected time."));
                        Stop(pid);
                    });
            }

            Task = _tcs.Task;
        }

        public PID Pid { get; }
        public Task<T> Task { get; }

        protected internal override void SendUserMessage(PID pid, object message)
        {
            var msg = MessageEnvelope.UnwrapMessage(message);

            if (msg is T || msg == null)
            {
                if (_cts != null && _cts.IsCancellationRequested)
                {
                    Stop(Pid);
                    return;
                }

                _tcs.TrySetResult((T) msg);
                Stop(Pid);
            }
            else
            {
                Stop(Pid);
                _tcs.SetException(
                    new InvalidOperationException(
                        $"Unexpected message. Was type {msg.GetType()} but expected {typeof(T)}"));
            }
        }

        protected internal override void SendSystemMessage(PID pid, object message)
        {
            if (message is Stop)
            {
                ProcessRegistry.Instance.Remove(Pid);
                _cts?.Dispose();
                return;
            }

            if (_cts == null || !_cts.IsCancellationRequested)
            {
                _tcs.TrySetResult(default(T));
            }

            Stop(pid);
        }
    }
}