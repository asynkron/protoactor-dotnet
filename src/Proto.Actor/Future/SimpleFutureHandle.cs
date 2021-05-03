// -----------------------------------------------------------------------
// <copyright file="SimpleFutureHandle.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Future
{
    sealed class SimpleFutureHandle : IFuture
    {
        private readonly Action _onTimeout;
        private readonly TaskCompletionSource<object> _tcs;

        public SimpleFutureHandle(PID pid, TaskCompletionSource<object> tcs, Action onTimeout)
        {
            _onTimeout = onTimeout;
            Pid = pid;
            _tcs = tcs;
        }

        public PID Pid { get; }
        public Task<object> Task => _tcs.Task;

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
                _onTimeout();
                throw new TimeoutException("Request didn't receive any Response within the expected time.");
            }
        }

        public void Dispose()
        {
        }
    }
}