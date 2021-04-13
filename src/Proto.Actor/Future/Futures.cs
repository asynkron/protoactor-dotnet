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
    public class FutureProcess : Process, IDisposable
    {
        private readonly CancellationTokenSource? _cts;
        private readonly TaskCompletionSource<object> _tcs;
        private readonly ActorMetrics? _metrics;
        private readonly ActorSystem _system;

        internal FutureProcess(ActorSystem system, CancellationToken cancellationToken = default) : base(system)
        {
            _system = system;

            if (!system.Metrics.IsNoop)
            {
                _metrics = system.Metrics.Get<ActorMetrics>();
                _metrics.FuturesStartedCount.Inc(new[] {system.Id, system.Address});
            }

            _tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (cancellationToken != default)
            {
                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            }

            var name = System.ProcessRegistry.NextId();
            var (pid, absent) = System.ProcessRegistry.TryAdd(name, this);
            
            if (!absent) throw new ProcessNameExistException(name, pid);

            Pid = pid;
            
            _cts?.Token.Register(
                () => {
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
                , false);

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
                System.ProcessRegistry.Remove(Pid);
                _cts?.Dispose();
                return;
            }

            if (_cts is null || !_cts.IsCancellationRequested) _tcs.TrySetResult(default!);

            if (!_system.Metrics.IsNoop)
            {
                _metrics!.FuturesCompletedCount.Inc(new[] {System.Id, System.Address});
            }

            Stop(pid);
        }

        public void Dispose()
        {
            System.ProcessRegistry.Remove(Pid);
            _cts?.Dispose();
        }
    }
}