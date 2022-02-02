// -----------------------------------------------------------------------
// <copyright file="Futures.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Proto.Metrics;

namespace Proto.Future
{
    public interface IFuture : IDisposable
    {
        public PID Pid { get; }
        public Task<object> Task { get; }

        public Task<object> GetTask(CancellationToken cancellationToken);
    }
    
    public sealed class FutureFactory
    {
        private ActorSystem System { get; }
        private readonly SharedFutureProcess? _sharedFutureProcess;

        public FutureFactory(ActorSystem system, bool useSharedFutures, int sharedFutureSize)
        {
            System = system;

            _sharedFutureProcess = useSharedFutures ? new SharedFutureProcess(system, sharedFutureSize) : null;
        }

        public IFuture Get() => _sharedFutureProcess?.TryCreateHandle() ?? SingleProcessHandle();

        private IFuture SingleProcessHandle() => new FutureProcess(System);
    }

    public sealed class FutureProcess : Process, IFuture
    {
        private readonly TaskCompletionSource<object> _tcs;
        private readonly KeyValuePair<string, object?>[] _metricTags = Array.Empty<KeyValuePair<string, object?>>();

        internal FutureProcess(ActorSystem system) : base(system)
        {
            if (system.Metrics.Enabled)
            {
                _metricTags = new KeyValuePair<string, object?>[] {new("id", System.Id), new("address", System.Address)};
                ActorMetrics.FuturesStartedCount.Add(1, _metricTags);
            }

            _tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            var name = System.ProcessRegistry.NextId();
            var (pid, absent) = System.ProcessRegistry.TryAdd(name, this);

            if (!absent) throw new ProcessNameExistException(name, pid);

            pid.RequestId = 1;
            Pid = pid;
        }

        public PID Pid { get; }
        public Task<object> Task => _tcs.Task;

        public async Task<object> GetTask(CancellationToken cancellationToken)
        {
            try
            {
                if (cancellationToken == default)
                {
                    return await _tcs.Task;
                }

                await using (cancellationToken.Register(() => _tcs.TrySetCanceled()))
                {
                    return await _tcs.Task;
                }
            }
            catch
            {
                if (System.Metrics.Enabled)
                    ActorMetrics.FuturesTimedOutCount.Add(1, _metricTags);

                Stop(Pid);
                throw new TimeoutException("Request didn't receive any Response within the expected time.");
            }
        }

        protected internal override void SendUserMessage(PID pid, object message)
        {
            try
            {
                _tcs.TrySetResult(message);
            }
            finally
            {
                if(System.Metrics.Enabled)
                    ActorMetrics.FuturesCompletedCount.Add(1, _metricTags);

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

            if (System.Metrics.Enabled)
                ActorMetrics.FuturesCompletedCount.Add(1, _metricTags);

            Stop(pid);
        }

        public void Dispose() => System.ProcessRegistry.Remove(Pid);
    }
}