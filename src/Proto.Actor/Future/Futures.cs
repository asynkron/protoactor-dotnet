// -----------------------------------------------------------------------
// <copyright file="Futures.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using Proto.Metrics;

namespace Proto.Future
{
    public sealed class FutureProcess : Process, IDisposable
    {
        private readonly TaskCompletionSource<object> _tcs;
        private readonly ActorMetrics? _metrics;
        private readonly ActorSystem _system;
        private readonly CancellationToken _ct;

        internal FutureProcess(ActorSystem system, CancellationToken cancellationToken = default) : base(system)
        {
            _system = system;

            if (!system.Metrics.IsNoop)
            {
                _metrics = system.Metrics.Get<ActorMetrics>();
                _metrics.FuturesStartedCount.Inc(new[] {system.Id, system.Address});
            }

            _tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            var name = System.ProcessRegistry.NextId();
            var (pid, absent) = System.ProcessRegistry.TryAdd(name, this);

            if (!absent) throw new ProcessNameExistException(name, pid);

            Pid = pid;
            
            if (cancellationToken != default)
            {
                cancellationToken.Register(() => {
                        if (_tcs.Task.IsCompleted) return;

                        _tcs.TrySetException(
                            new TimeoutException("Request didn't receive any Response within the expected time.")
                        );

                        if (!system.Metrics.IsNoop)
                        {
                            _metrics!.FuturesTimedOutCount.Inc(new[] {System.Id, system.Address});
                        }

                        Stop(pid);
                    }
                    , false
                );
            }

            _ct = cancellationToken;

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
                if (!_system.Metrics.IsNoop)
                {
                    _metrics!.FuturesCompletedCount.Inc(new[] {System.Id, System.Address});
                }
                
                Stop(Pid);
            }
        }

        protected internal override void SendSystemMessage(PID pid, object message)
        {
            if (message is Stop)
            {
                Dispose();
                return;
            }

            if (_ct == default || !_ct.IsCancellationRequested) _tcs.TrySetResult(default!);

            if (!_system.Metrics.IsNoop)
            {
                _metrics!.FuturesCompletedCount.Inc(new[] {System.Id, System.Address});
            }

            Stop(pid);
        }

        public void Dispose() => System.ProcessRegistry.Remove(Pid);
    }
}