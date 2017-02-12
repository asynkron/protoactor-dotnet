// -----------------------------------------------------------------------
//  <copyright file="Futures.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;

namespace Proto
{
    public class FutureProcess<T> : Process
    {
        private readonly TaskCompletionSource<T> _tcs;

        public FutureProcess(TaskCompletionSource<T> tcs)
        {
            _tcs = tcs;
        }

        public override void SendUserMessage(PID pid, object message, PID sender)
        {
            if (message is T)
            {
                _tcs.TrySetResult((T) message);
                pid.Stop();
            }
        }

        public override void SendSystemMessage(PID pid, object message)
        {
        }
    }
}