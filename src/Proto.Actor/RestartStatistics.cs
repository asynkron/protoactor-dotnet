// -----------------------------------------------------------------------
//  <copyright file="RestartStatistics.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;

namespace Proto
{
    public class RestartStatistics
    {
        public int FailureCount { get; private set; }
        public DateTime? LastFailureTime { get; private set; }

        public RestartStatistics(int failureCount, DateTime? lastFailuretime)
        {
            FailureCount = failureCount;
            LastFailureTime = lastFailuretime;
        }

        public void Fail()
        {
            FailureCount++;
        }

        public void Reset()
        {
            FailureCount = 0;
        }

        public void Restart()
        {
            LastFailureTime = DateTime.Now;
        }

        public bool IsWithinDuration(TimeSpan within)
        {
            return (DateTime.Now - LastFailureTime) < within;
        }
    }
}