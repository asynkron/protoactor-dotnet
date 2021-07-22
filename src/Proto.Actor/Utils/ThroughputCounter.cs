// -----------------------------------------------------------------------
// <copyright file="ThroughputCounter.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;

namespace Proto.Utils
{
    public class ThroughputCounter : IDisposable
    {
        private int _counter;
        private readonly Timer _timer;

        public ThroughputCounter(TimeSpan interval, Action<int> throughputAction)
        {
            var last = DateTime.Now;
            _timer = new Timer(_ =>
            {
                var now = DateTime.Now;
                var diff = (now - last).TotalMilliseconds;
                last = now;
                
                var throughput = _counter * 1000.0 / diff;
                _counter = 0;
                throughputAction((int) throughput);
            }, null, interval, interval);
        }

        public void Tick() => Interlocked.Increment(ref _counter);

        public void Tick(int count) => Interlocked.Add(ref _counter, count);

        public void Dispose() => _timer.Dispose();
    }
}