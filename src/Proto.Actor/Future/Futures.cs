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

        internal FutureProcess(ActorSystem system) : base(system)
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
        }

        public PID Pid { get; }
               
        public Task<object> GetTask() => _tcs.Task;

        public async Task<object> GetTask(CancellationToken cancellationToken)
        {
            try
            {
                await using (cancellationToken.Register(() => _tcs.TrySetCanceled()))
                {
                    return await _tcs.Task;
                }
            }
            catch
            {
                if (!_system.Metrics.IsNoop)
                {
                    _metrics!.FuturesTimedOutCount.Inc(new[] {System.Id, _system.Address});
                }

                Stop(Pid!);
                throw new TimeoutException("Request didn't receive any Response within the expected time.");
            }
        }

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

            _tcs.TrySetResult(default!);

            if (!_system.Metrics.IsNoop)
            {
                _metrics!.FuturesCompletedCount.Inc(new[] {System.Id, System.Address});
            }

            Stop(pid);
        }

        public void Dispose() => System.ProcessRegistry.Remove(Pid);
    }
}