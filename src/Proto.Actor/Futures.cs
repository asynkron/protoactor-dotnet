// -----------------------------------------------------------------------
//  <copyright file="Futures.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;

namespace Proto
{
    public class FutureProcess<T> : Process
    {
        private readonly TaskCompletionSource<T> _tcs;
        private readonly CancellationToken _cancellationToken;

        public FutureProcess(TaskCompletionSource<T> tcs) : this(tcs, CancellationToken.None) { }
        public FutureProcess(TaskCompletionSource<T> tcs, CancellationToken cancellationToken)
        {
            _tcs = tcs;
            _cancellationToken = cancellationToken;
        }

        public override void SendUserMessage(PID pid, object message, PID sender)
        {
            if (message is T)
            {
                if (_cancellationToken.IsCancellationRequested) return;

                _tcs.TrySetResult((T)message);
                pid.Stop();
            }
        }

        public override void SendSystemMessage(PID pid, object message)
        {
        }
    }
}