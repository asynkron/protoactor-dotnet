// -----------------------------------------------------------------------
//   <copyright file="Futures.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Future
{
    internal class FutureProcess : Process
    {
        private readonly CancellationTokenSource? _cts;
        private readonly TaskCompletionSource<object> _tcs;

        internal FutureProcess(ActorSystem system, TimeSpan timeout) : this(system, new CancellationTokenSource(timeout)
        )
        {
        }

        internal FutureProcess(ActorSystem system, CancellationToken cancellationToken)
            : this(system, CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
        }

        internal FutureProcess(ActorSystem system) : this(system, null)
        {
        }

        private FutureProcess(ActorSystem system, CancellationTokenSource? cts) : base(system)
        {
            _tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            _cts = cts;

            var name = System.ProcessRegistry.NextId();
            var (pid, absent) = System.ProcessRegistry.TryAdd(name, this);

            if (!absent)
            {
                throw new ProcessNameExistException(name, pid);
            }

            Pid = pid;

            _cts?.Token.Register(
                () =>
                {
                    if (_tcs.Task.IsCompleted)
                    {
                        return;
                    }

                    _tcs.TrySetException(
                        new TimeoutException("Request didn't receive any Response within the expected time.")
                    );

                    Stop(pid);
                }
            );
            Task = _tcs.Task;
        }

        public PID Pid { get; }
        public Task<object> Task { get; }

        protected internal override void SendUserMessage(PID pid, object message)
        {
            try
            {
                _tcs.TrySetResult(MessageEnvelope.UnwrapMessage(message)!);
            }
            finally
            {
                Stop(Pid);
            }
        }

        protected internal override void SendSystemMessage(PID pid, object message)
        {
            if (message is Stop)
            {
                System.ProcessRegistry.Remove(Pid);
                _cts?.Dispose();
                return;
            }

            if (_cts == null || !_cts.IsCancellationRequested)
            {
                _tcs.TrySetResult(default!);
            }

            Stop(pid);
        }
    }
}